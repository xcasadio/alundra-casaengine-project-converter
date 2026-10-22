"""Writes vectors/mapping.json: the table binary address -> logical field -> DLL member the vectors use, per opcode, with the observed
read and write sets (union over all vectors, from read-write-sets.json and the zero-site files). Deterministic."""
import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sm4model as S
out_dir = sys.argv[1]
rw = json.load(open(os.path.join(out_dir, 'read-write-sets.json')))
doc = {
 'note': 'An address the handler reads or writes that is not in this table stops the generator (UNMAPPED): none was met.',
 'slotFields': [dict(offset='+0x%X' % off, field=f[0], dllMember=f[1], encoding=f[2]) for off, f in sorted(S.SLOT_FIELDS.items())],
 'slotOnlyForTheZeroSiteOpcodes': [
    dict(offset='+0x44', field='recordPtr / record byte 9', dllMember='EntityScriptProxy.RecordHeight (int?; null = no record = fatal message in the binary)', opcode='0x15'),
    dict(offset='+0x1EC', field='modZ', dllMember='EntityScriptProxy.ModZ (int)', opcode='0x15'),
    dict(offset='+0x11C', field='posZ', dllMember='EntityScriptProxy.PosZ (int, 16.16; the DLL frame is one raw unit below the binary, ADR-0026)', opcode='0x15'),
    dict(offset='+0x234..+0x26B', field='ownerState.*', dllMember='the OWNER proxy\'s EventProgramState (CopyFrom of the shared scratch state)', opcode='0x66')],
 'stateFields': [dict(offset='+0x%X' % off, field=f[0], dllMember=f[1]) for off, f in sorted(S.STATE_FIELDS.items())] + [dict(address='0x8013D9E8', note='the SHARED state of the programs A, D, E, F (the binary passes it as a3); the DLL equivalent is EventProgramRunner._slotAScratchState (SharedProgramStateForTests)')],
 'statsFields': [dict(offset='+%d' % off, field=f[0], dllMember=f[1]) for off, f in sorted(S.STATS_FIELDS.items())] + [dict(address='[0x80176318] -> PlayerStats', dllMember='GameState.PlayerStats')],
 'padFields': [dict(address='0x80126F%02X' % (0x18 + off), field=f[0], dllMember=f[1]) for off, f in sorted(S.PAD_FIELDS.items())] + [dict(address='0x80126F18..0x80126F27', field='counters (+0..+0xF)', dllMember='TickPad.RepeatInterval / IsOverThanMaxNbFrameHeld / NumberOfFrameHold (never read nor written by 0xB3)')],
 'otherAddresses': [
    dict(address='0x80134360', field='N = index of the last used slot', dllMember='IEntityWorldContext.SpawnedEntities.Count - 1 (the hero is the first element)'),
    dict(address='0x80127D30 + 0x294*i', field='slot i', dllMember='SpawnedEntities[i]; slot 0 = IEntityWorldContext.PlayerEntity (IsPlayer = true, EntityRefId = -1)'),
    dict(address='0x8013D8D4..', field='match buffer (internal to the search)', dllMember='the List<EntityScriptProxy> that EntitySearchService returns'),
    dict(address='0x80023C30', field='search switch table (12 entries)', dllMember='the switch of EntitySearchService.GetMatchingEntitiesBySearchType'),
    dict(address='0x80023D2C', field='status table, 3 words {2, 3, 4}', dllMember='a new static table {EntityStatus.Normal, Deactivated, FlagToDestroy}; an index above 2 reads past it (policy R2-3)'),
    dict(address='a2 = &cursor', field='the instruction pointer', dllMember='EventProgramState.CodeIndex / the int[] v of Dispatch'),
    dict(address='stack 0x801F....', field='saved registers', dllMember='none (internal)')],
 'readWriteSets': rw,
}
text = json.dumps(doc, indent=1, sort_keys=False) + '\n'
open(os.path.join(out_dir, 'mapping.json'), 'w', encoding='utf-8', newline='\n').write(text)
print('mapping.json written')
