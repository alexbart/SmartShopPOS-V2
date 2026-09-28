# Barcode Scanner

Known device: Syble XB-3120.

- USB HID keyboard-wedge behavior
- Barcode input is followed by Enter
- Initial architecture should treat it as keyboard input
- A vendor SDK should not be required initially

Scanner-specific behavior should remain outside core business logic where practical.
