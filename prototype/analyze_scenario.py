#!/usr/bin/env python3
"""Analyze packet capture log: extract scenarios and trace dynamic fields."""

import json, re, sys, os

# --- Categories ---
ESSENTIAL = {'CS_LOGIN','SC_LOGIN_RESULT','CS_CHAR_LIST','SC_CHAR_LIST',
             'CS_CHAR_SELECT','SC_CHAR_INFO','CS_CHAR_CREATE','SC_CHAR_CREATE_RESULT'}
NOISE = {'SC_NPC_SPAWN','SC_NPC_DEATH','SC_EXP_UPDATE','SC_LEVEL_UP',
         'SC_ATTENDANCE_INFO','SC_HEARTBEAT','CS_HEARTBEAT','SC_ERROR'}
GAMEPLAY_PAIRS = {'CS_MOVE','SC_MOVE_RESULT','CS_ATTACK','SC_ATTACK_RESULT',
                  'CS_ATTENDANCE_CHECK','SC_ATTENDANCE_RESULT'}

# --- Phase assignment ---
PHASE_MAP = {
    'CS_LOGIN': 'login', 'SC_LOGIN_RESULT': 'login',
    'CS_CHAR_LIST': 'character_select', 'SC_CHAR_LIST': 'character_select',
    'CS_CHAR_SELECT': 'character_select', 'SC_CHAR_INFO': 'character_select',
    'CS_CHAR_CREATE': 'character_select', 'SC_CHAR_CREATE_RESULT': 'character_select',
}

# --- Dynamic field rules ---
DYNAMIC_RULES = [
    {'packet':'CS_LOGIN','field':'accountId','source':'csv','reason':'Login credential - must be unique per client'},
    {'packet':'CS_LOGIN','field':'password','source':'csv','reason':'Login credential'},
    {'packet':'CS_CHAR_SELECT','field':'charUid','source':'response',
     'source_packet':'SC_CHAR_LIST','source_field':'chars[0].charUid',
     'reason':'Character UID from server response'},
    {'packet':'CS_CHAR_CREATE','field':'charUid','source':'response',
     'source_packet':'SC_LOGIN_RESULT','source_field':'accountUid',
     'reason':'Account UID from login response'},
    {'packet':'CS_CHAR_CREATE','field':'name','source':'csv','reason':'Character name - user input'},
    {'packet':'CS_CHAR_CREATE','field':'charType','source':'static','reason':'Fixed character type selection'},
    {'packet':'CS_ATTACK','field':'targetUid','source':'response',
     'source_packet':'SC_NPC_SPAWN','source_field':'npcUid',
     'reason':'NPC UID from spawn notification'},
    {'packet':'CS_MOVE','field':'dirX','source':'static','reason':'Movement direction value'},
    {'packet':'CS_MOVE','field':'dirY','source':'static','reason':'Movement direction value'},
]

def categorize(name):
    if name in ESSENTIAL: return 'essential'
    if name in GAMEPLAY_PAIRS: return 'gameplay'
    if name in NOISE: return 'noise'
    return 'noise'

def parse_log(path):
    packets = []
    pkt = None
    header_re = re.compile(r'^\[(\S+)\]\s+(SEND|RECV)\s+(\S+)\s+\((\d+)\s+bytes\)')
    route_re = re.compile(r'^\s+(\S+)\s+->\s+(\S+)')
    field_re = re.compile(r'^\s+(\w+):\s+(.*)')

    for line in open(path, encoding='utf-8'):
        m = header_re.match(line)
        if m:
            if pkt: packets.append(pkt)
            pkt = {'time': m[1], 'direction': m[2], 'name': m[3], 'bytes': int(m[4]), 'fields': {}}
            continue
        if not pkt: continue
        m = route_re.match(line)
        if m: continue
        m = field_re.match(line)
        if m:
            k, v = m[1], m[2].strip()
            if k == 'raw': continue
            if v.startswith('"') and v.endswith('"'): v = v[1:-1]
            else:
                try: v = int(v)
                except ValueError: pass
            pkt['fields'][k] = v
    if pkt: packets.append(pkt)
    return packets

def build_phases(packets):
    phase_order = ['login', 'character_select', 'gameplay']
    phase_pkts = {p: [] for p in phase_order}
    for p in packets:
        cat = categorize(p['name'])
        if cat == 'noise': continue
        phase = PHASE_MAP.get(p['name'], 'gameplay')
        phase_pkts[phase].append({
            'name': p['name'], 'direction': p['direction'],
            'category': cat, 'fields': p['fields'], 'time': p['time']
        })
    return [{'name': ph, 'packets': phase_pkts[ph]} for ph in phase_order if phase_pkts[ph]]

def find_dynamic_fields(packets, protocol):
    # Build protocol field map
    proto_fields = {}
    for pdef in protocol['packets']:
        proto_fields[pdef['name']] = [f['name'] for f in pdef.get('fields', [])]

    # Only emit rules for packets actually seen
    seen = {p['name'] for p in packets}
    result = []
    for rule in DYNAMIC_RULES:
        if rule['packet'] in seen and rule['field'] in proto_fields.get(rule['packet'], []):
            result.append(rule)
    return result

def main():
    base = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.dirname(base)
    log_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        repo, 'PacketCaptureAgent/bin/Debug/net9.0/capture_20260313_231631.log')
    proto_path = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        repo, 'protocols/mmorpg_simulator.json')

    protocol = json.load(open(proto_path, encoding='utf-8'))
    packets = parse_log(log_path)

    phases = build_phases(packets)
    dynamic = find_dynamic_fields(packets, protocol)
    noise_names = sorted({p['name'] for p in packets if categorize(p['name']) == 'noise'})

    counts = {'essential': 0, 'gameplay': 0, 'noise': 0}
    for p in packets:
        counts[categorize(p['name'])] += 1

    result = {
        'phases': phases,
        'dynamic_fields': dynamic,
        'noise_filtered': noise_names,
        'summary': {'total_packets': len(packets), **counts}
    }

    out_path = os.path.join(base, 'analysis_result.json')
    json.dump(result, open(out_path, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)

    # Human-readable summary
    print(f"=== Packet Capture Analysis ===")
    print(f"Total: {len(packets)} packets  |  Essential: {counts['essential']}  |  Gameplay: {counts['gameplay']}  |  Noise: {counts['noise']}")
    print(f"\n--- Phases ---")
    for ph in phases:
        print(f"  [{ph['name']}] {len(ph['packets'])} packets")
        for p in ph['packets'][:5]:
            print(f"    {p['time']} {p['direction']:4s} {p['name']}")
        if len(ph['packets']) > 5:
            print(f"    ... +{len(ph['packets'])-5} more")
    print(f"\n--- Noise Filtered ---")
    print(f"  {', '.join(noise_names)}")
    print(f"\n--- Dynamic Fields ---")
    for d in dynamic:
        src = d['source']
        extra = f" <- {d.get('source_packet','')}.{d.get('source_field','')}" if src == 'response' else ''
        print(f"  {d['packet']}.{d['field']}: [{src}]{extra} - {d['reason']}")
    print(f"\nOutput saved to: {out_path}")

if __name__ == '__main__':
    main()
