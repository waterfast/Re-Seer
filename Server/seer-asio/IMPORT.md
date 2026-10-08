# Imported server and first-stage integration

Copied from the adjacent `seer-asio` repository, commit `04a6b7a`.
The original repository is not modified; this directory is ordinary tracked source,
not a nested Git repository or a submodule.

The imported commit contains an unfinished battle-core rewrite. The runtime restores
these modules from its previous commit `09790b5`:

- `lua/core/skill.lua`, `lua/core/timing.lua`, `lua/core/trigger_data.lua`
- `lua/core/effect/init.lua`
- The seven modules under `lua/server/battle/`

The unfinished replacement `skill.lua` is retained under `experimental-rebuild/`;
the other experimental events and classes remain in their original imported paths.
They are not loaded by the first-stage runtime.

Integration changes add `src/battle/` and `lua/server/rpc/battle_api.lua`, validate
player ownership in the internal dispatchers, and move PP consumption from the
direct-call facade to `GameEvent.UseSkill`, which is also used by normal rounds.

Build and run instructions, protocol and acceptance scope are in
[`../../docs/联机第一阶段.md`](../../docs/联机第一阶段.md).

Existing source copyright and SPDX notices are preserved.
