# SyncTool

Windows x64 本機資料夾與 Google Drive 桌面版掛載資料夾的安全同步工具。**不使用 Google Drive API 或 OAuth**；雲端傳輸只由 Google Drive 桌面版負責。

## 核心安全設計

- 每個工作有唯一 ID，且 A/B 不得重複、相同或互為父子資料夾。
- 模式化同步基準：`state\two-way.json`、`state\a-to-b.json`、`state\b-to-a.json`；升級舊 `state.json` 時保留時間戳 legacy 副本。
- 覆寫使用同資料夾暫存檔、大小與串流 SHA-256 驗證、舊版備份、原子替換。
- state／preview 無效或毀損時 fail-closed：不寫入 A/B 或 state；毀損 preview 原檔會保存到 `previews\diagnostics\`。
- 所有工作使用跨程序 Mutex，避免 GUI、排程與 CLI 同時同步。

## 模式

| 模式 | CLI 值 | 行為 |
|---|---|---|
| 智慧雙向 | `two-way` | 以各模式基準判斷單側更新、刪除及衝突。 |
| A → B | `a-to-b` | A 為主端；首次／改模式必須預覽確認。 |
| B → A | `b-to-a` | B 為主端；首次／改模式必須預覽確認。 |
| 僅檢查 | `preview` | 只掃描與輸出計畫，不寫入。 |

單向 Preview 產生隨機 `previewId`，綁定模式、端點與掃描指紋，30 分鐘後失效。確認前若任何項目變動，寫入會被拒絕。已人工確認的單向工作可由排程自動執行；若高風險操作超過設定門檻（預設 100 項或 10 GiB），會重新要求確認。

## GUI

無參數啟動 `SyncTool.exe`：

- 管理多組工作、排程與每組預設模式。
- 單向執行顯示 preview ID、到期時間與風險數量後才可確認。
- 可取消目前工作；取消不更新同步基準，已完成原子替換會保留。
- `處理待決衝突`：逐項選擇 A → B、B → A、保留兩份或略過。
- `預覽備份清理`：先列出過期備份；只有再次確認後才會清理，並寫入 `jobs\<工作ID>\logs\backup-cleanup-audit.log`。

## CLI

必須指定一組工作，不會自行同步全部工作：

```text
SyncTool.exe --dry-run --job="工作名稱" --mode=a-to-b
SyncTool.exe --run-once --job="工作名稱" --mode=a-to-b --confirm-preview="<previewId>"
SyncTool.exe --run-once --job="工作名稱" --mode=two-way
SyncTool.exe --config="C:\path\config.json" --dry-run --job="工作名稱或ID" --mode=preview
```

`--dry-run` 與 `--run-once` 必須二擇一。stdout 僅輸出一個 JSON；成功 exit code `0`、同步失敗 `1`、參數錯誤 `2`。

## 資料位置

發布目錄旁：

```text
config.json
logs\sync-YYYYMMDD.log
jobs\<工作ID>\
  state\
  previews\
  pending-conflicts.json
  backups\
  logs\
```

建議先在 GUI 建立工作、使用「測試路徑」確認 A/B，再啟用排程。不要將發布目錄、工作 state、log 或 backup 目錄設為 A 或 B。
