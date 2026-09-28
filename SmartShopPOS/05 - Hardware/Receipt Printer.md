# Receipt Printer

Known device: Macro80F.

- Uses the Windows printer queue
- Configured as Generic / Text Only
- Windows currently uses USB001
- RAW ESC/POS printing has been tested successfully

Future printing should be represented through an abstraction such as `IReceiptPrinter` and handled by the local Windows hardware agent.
