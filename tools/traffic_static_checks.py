"""Source/metadata checks only. Does not compile C# or execute Unity tests.

Requires tree-sitter and tree-sitter-c-sharp in the selected Python environment.
Run from the repository root; optional argument is the comparison Git ref.
"""
from pathlib import Path
import re
import subprocess
import sys
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

parser = Parser(Language(tree_sitter_c_sharp.language()))
sources = list(Path('Assets/_Project/Scripts').rglob('*.cs'))
errors = [str(path) for path in sources if parser.parse(path.read_bytes()).root_node.has_error]
assert not errors, errors
print(f'C# syntax only: {len(sources)} files, zero syntax errors; NOT compilation')

guids = {}
metas = list(Path('Assets').rglob('*.meta'))
for path in metas:
    match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.MULTILINE)
    assert match, f'Missing or malformed GUID: {path}'
    guid = match.group(1)
    assert guid not in guids, f'Duplicate GUID: {path}, {guids.get(guid)}'
    guids[guid] = path
for path in sources:
    assert Path(str(path) + '.meta').exists(), f'Missing C# metadata: {path}'
print(f'Metadata: {len(metas)} unique GUIDs; all C# sources have .meta files')

base = sys.argv[1] if len(sys.argv) > 1 else 'origin/main'
changed = subprocess.check_output(['git', 'diff', '--name-only', base], text=True).splitlines()
protected = [name for name in changed if name.endswith(('.unity', '.prefab', '.asset', '.asmdef'))
             or name.startswith(('Packages/', 'ProjectSettings/'))]
assert not protected, f'Unexpected scene/asset/package/assembly changes: {protected}'
subprocess.run(['git', 'diff', '--check', base], check=True)
print(f'Protected scene/assets/packages/assemblies unchanged against {base}; whitespace clean')

for suite in ('CarFollowingTests', 'RoadTurnPathTests', 'TrafficSignalTests', 'TrafficBehaviorIntegrationTests',
              'TaxiStopAnchorTests', 'CurbTaxiIntegrationTests', 'RideLifecycleServiceTests',
              'RideDemandServiceTests', 'RideGroundMetricTests', 'TaxiShiftPresenterTests', 'DialoguePresenterTests'):
    source = Path(f'Assets/_Project/Scripts/Tests/EditMode/{suite}.cs').read_text()
    cases = source.count('[Test]') + source.count('[TestCase(')
    print(f'{suite}: {cases} authored cases; NOT EXECUTED')
