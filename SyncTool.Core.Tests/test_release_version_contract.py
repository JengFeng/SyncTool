from pathlib import Path
project = Path(__file__).resolve().parents[1] / 'SyncTool.App' / 'SyncTool.App.csproj'
source = project.read_text(encoding='utf-8')
for expected in ('<Version>1.1.1</Version>', '<AssemblyVersion>1.1.1.0</AssemblyVersion>', '<FileVersion>1.1.1.0</FileVersion>', '<InformationalVersion>1.1.1</InformationalVersion>'):
    if expected not in source:
        raise SystemExit(f'MISSING_VERSION_FIELD: {expected}')
print('SYNCTOOL_RELEASE_VERSION_CONTRACT_OK')
