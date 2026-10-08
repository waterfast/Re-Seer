-- Stage-one client adapter. The rules remain in BattleLogic; this module supplies
-- a fixed local demo, checks prompts, drives the AI, and projects display snapshots.
local M = {}

local function installDemo()
  local stats = { hp = 110, attack = 100, defense = 90, sp_attack = 100, sp_defense = 90, speed = 100 }
  Seer:addSpecies{ id = 4500, name = "联调武心婵", elements = { "电" }, base_stats = stats }
  Seer:addSpecies{ id = 4260, name = "联调湮灵", elements = { "战斗" }, base_stats = stats }
  -- IDs match the Unity demonstration catalog. These are explicitly test skills.
  Seer:createSkill{ id = 900001, name = "demo_thunder", element = "电", category = Skill.Physical,
    power = 120, pp = 10, accuracy = 100, target = "enemy" }
  Seer:createSkill{ id = 900002, name = "demo_break", element = "战斗", category = Skill.Physical,
    power = 90, pp = 15, accuracy = 100, target = "enemy",
    effects = { { kind = "clear_stages", side = "up" } } }
  Seer:createSkill{ id = 900003, name = "demo_focus", element = "普通", category = Skill.Status,
    pp = 20, accuracy = 100, target = "self",
    effects = { { kind = "stat", target = "self", stages = { attack = 2 } } } }
  Seer:createSkill{ id = 900004, name = "demo_spark", element = "电", category = Skill.Special,
    power = 65, pp = 25, accuracy = 100, target = "enemy",
    effects = { { kind = "mark", mark = "paralysis", probability = 30 } } }
  Seer:postLoad()
end

local function skillSlot(pet, skill, canAct)
  local usable = skill:checkUsable(pet)
  return { skillId = skill.name, currentPp = pet:getPP(skill.name), maxPp = skill.pp,
    available = canAct and usable == true }
end

local function snapshot(session)
  local logic = session.logic
  local request = logic.pending_request
  local finished = logic.game_over == true
  local canAct = not finished and request ~= nil and request.pet == 1
  local function petView(p)
    return { petId = "pet-" .. p.seat, speciesId = tostring(p.species.id),
      name = p.seat == 1 and "武心婵" or "湮灵", level = p.level,
      elementIds = { p.seat == 1 and "88" or "90" }, currentHp = p.hp, maxHp = p.max_hp }
  end
  local self = session.pets[1]
  local slots = table.map(self:getSkills(), function(skill) return skillSlot(self, skill, canAct) end)
  local winner
  for playerId, player in pairs(session.players) do
    if player.side == logic.winner then winner = tostring(playerId) end
  end
  return { battleId = tostring(session.room_id), sequence = session.clientSequence,
    round = logic.round, finished = finished, winnerPlayerId = winner or "",
    canAct = canAct, promptId = canAct and request.seq or 0,
    selfPet = petView(self), enemyPet = petView(session.pets[2]), skills = slots,
    fifthSkill = self:getFifthSkill() and skillSlot(self, self:getFifthSkill(), canAct) or nil }
end

function M.install(d)
  installDemo()
  local function getSession(params)
    local session, err = Session.get(params.roomId)
    if session == nil then return nil, err end
    if params.battleId ~= tostring(session.room_id) then return nil, "战斗编号不匹配" end
    return session
  end

  local function driveAi(session, task)
    for _ = 1, 200 do
      if task.finished or not task.ask or task.ask.pet == 1 then return true end
      local ask = task.ask
      local action
      if ask.kind == "AskForAction" then
        action = { type = "UseSkill", skillName = ask.skills[1], targetId = 1 }
      elseif ask.kind == "AskForChoice" then
        action = { type = "Choose", choice = ask.choices[1] }
      else return false, "未知 AI 询问" end
      local ok, nextTask = d.handlePlayerAction{ roomId = session.room_id, playerId = 2, action = action }
      if not ok then return false, "AI 操作失败" end
      task = nextTask
    end
    return false, "AI 推进超过上限"
  end

  function d.clientStartGame(params)
    if Session.sessions[params.roomId] then return false, "invalid_params", "战斗已经存在" end
    local ok, err = d.startGame{ roomId = params.roomId, seed = "unity-stage1-v1", players = {
      { playerId = 1, pets = { { species = "联调武心婵", level = 100,
        skills = { "demo_thunder", "demo_break", "demo_focus" }, fifth = "demo_spark" } } },
      { playerId = 2, pets = { { species = "联调湮灵", level = 100,
        skills = { "demo_break" } } } },
    } }
    if not ok then return false, "internal_error", tostring(err) end
    local session = Session.sessions[params.roomId]
    session.clientSequence = 1
    local started, task = d.runGame{ roomId = params.roomId }
    if not started then return false, "internal_error", "无法推进战斗" end
    local driven, reason = driveAi(session, task)
    if not driven then return false, "internal_error", reason end
    return true, snapshot(session)
  end

  function d.clientSnapshot(params)
    local session, err = getSession(params)
    if not session then return false, "invalid_params", err end
    return true, snapshot(session)
  end

  function d.clientAction(params)
    local session, err = getSession(params)
    if not session then return false, "invalid_params", err end
    if type(params.action) ~= "table" then return false, "invalid_params", "缺少 action" end
    local encoded = require("json").encode(params.action)
    -- Retransmission of an accepted prompt returns its state without repeating rules or events.
    if params.promptId == session.lastClientPrompt then
      if encoded ~= session.lastClientAction then return false, "invalid_params", "同一询问不能更改操作" end
      return true, snapshot(session)
    end
    local ask = session.logic.pending_request
    if session.logic.game_over or not ask or ask.pet ~= 1 or params.promptId ~= ask.seq then
      return false, "invalid_params", "询问已过期或不属于当前玩家"
    end
    local action = params.action
    local reply
    if ask.kind == "AskForAction" and action.type == "UseSkill" then
      local skill = session.pets[1]:getSkill(action.skillId)
      if skill == nil or not skill:checkUsable(session.pets[1]) then
        return false, "invalid_params", "该技能不可用"
      end
      local target = skill.target == "self" and 1 or 2
      if action.targetPetId ~= "pet-" .. target then return false, "invalid_params", "非法目标" end
      reply = { type = "UseSkill", skillName = skill.name, targetId = target }
    elseif ask.kind == "AskForChoice" then
      if not table.find(ask.choices or {}, function(c) return c == action.choice end) then
        return false, "invalid_params", "非法选项"
      end
      reply = { type = "Choose", choice = action.choice }
    else return false, "invalid_params", "不支持此操作" end
    local ok, task = d.handlePlayerAction{ roomId = params.roomId, playerId = 1, action = reply }
    if not ok then return false, "internal_error", "操作执行失败" end
    local driven, reason = driveAi(session, task)
    if not driven then return false, "internal_error", reason end
    session.lastClientPrompt, session.lastClientAction = params.promptId, encoded
    session.clientSequence = session.clientSequence + 1
    return true, snapshot(session)
  end
end

return M
