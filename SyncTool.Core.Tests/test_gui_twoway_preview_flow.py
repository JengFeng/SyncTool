from pathlib import Path
source = (Path(__file__).resolve().parents[1] / 'SyncTool.App' / 'MainForm.cs').read_text(encoding='utf-8')
if 'if (dryRun || mode is SyncMode.TwoWay or SyncMode.Preview)' in source:
    raise SystemExit('TWOWAY_BYPASSES_PREVIEW_ID_FLOW')
if '確認單向同步預覽' in source:
    raise SystemExit('TWOWAY_CONFIRMATION_LABEL_IS_INCORRECT')
required = 'if (dryRun || mode is SyncMode.Preview)'
if required not in source:
    raise SystemExit('PREVIEW_ONLY_BRANCH_MISSING')
if '確認同步預覽' not in source:
    raise SystemExit('GENERIC_CONFIRMATION_LABEL_MISSING')
print('GUI_TWOWAY_PREVIEW_FLOW_CONTRACT_OK')
