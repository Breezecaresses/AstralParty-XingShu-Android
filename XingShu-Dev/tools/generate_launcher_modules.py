"""Compile the launcher's canonical module catalog for the stripped Unity framework."""
from pathlib import Path
import json
import re
import sys


def main(destination: Path) -> None:
    root = Path(__file__).resolve().parents[2]
    catalog = json.loads((root / 'AstralParty-Hub/src/shared/launcherModules.json').read_text('utf-8'))
    ids, types = set(), set()
    rows = []
    for mod in catalog:
        key = mod['id']
        assert re.fullmatch('[a-z0-9-]{1,48}', key) and key not in ids
        ids.add(key)
        for name in mod['types']:
            assert re.fullmatch('[A-Za-z][A-Za-z0-9]{1,60}', name) and name not in types
            types.add(name)
        values = ', '.join(json.dumps(value) for value in [key, *mod['types']])
        rows.append('            new[] { ' + values + ' },')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text('''// Generated from launcherModules.json.
namespace XingShuRuntime.Core
{
    internal static class LauncherModuleCatalog
    {
        internal static readonly string[][] Modules =
        {
''' + '\n'.join(rows) + '''
        };
    }
}
''', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main(Path(sys.argv[1]))
