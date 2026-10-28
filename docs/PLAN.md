# PadPilot – Plan für Schritt 1 (Erkennung + Live-Anzeige)

## Projektstruktur

```
PadPilot.sln
src/PadPilot.Core/          Klassenbibliothek, kein UI
  ControllerState.cs        Zustand eines Frames (Tasten, Sticks, Trigger, Touch, Gyro, Akku)
  InputReportParser.cs      Rohbytes -> ControllerState (USB + Bluetooth)
  DualSenseDevice.cs        Gerät finden, öffnen, Lese-Thread, BT-Vollmodus
src/PadPilot.App/           WPF-Oberfläche (.NET 8)
  MainViewModel.cs          Verbindung, Hotplug, UI-Aktualisierung mit ~60 Hz
  MainWindow.xaml           Live-Anzeige
tests/PadPilot.Core.Tests/  xUnit-Tests für den Parser (ohne Controller lauffähig)
```

## NuGet-Pakete

| Paket | Wofür |
|---|---|
| HidSharp 2.1.0 | HID-Zugriff unter Windows (USB und Bluetooth) |
| xunit | Tests |
| später: Nefarius.ViGEm.Client | Xbox-360-Emulation (Schritt 3) |

## Gerät erkennen

- Vendor ID `0x054C` (Sony), Product ID `0x0CE6` (DualSense) bzw. `0x0DF2` (DualSense Edge)
- USB oder Bluetooth anhand der maximalen Input-Report-Länge: **64 Byte = USB**, **78 Byte = Bluetooth**
- Hotplug über `DeviceList.Local.Changed`

## Input-Report

| | USB | Bluetooth |
|---|---|---|
| Report-ID | `0x01` | `0x31` |
| Länge | 64 | 78 |
| Daten ab Byte | 1 | 2 (Byte 1 = Sequenz/Tag) |
| Prüfsumme | – | CRC32 in den letzten 4 Byte |

Bluetooth schickt anfangs nur einen verkürzten Report `0x01`. Erst nach dem Lesen des
Feature-Reports `0x05` (Kalibrierung) schaltet der Controller auf den vollen Report `0x31` um.

Aufbau der Daten (Offset relativ zum Datenbeginn, Quelle: Linux `hid-playstation`):

| Offset | Inhalt |
|---|---|
| 0–3 | Linker Stick X/Y, rechter Stick X/Y (0–255, 128 = Mitte) |
| 4–5 | L2, R2 analog (0–255) |
| 6 | Sequenzzähler |
| 7 | Bits 0–3: D-Pad (0 = oben, im Uhrzeigersinn bis 7, 8 = nichts); Bit 4–7: Quadrat, Kreuz, Kreis, Dreieck |
| 8 | L1, R1, L2, R2, Create, Options, L3, R3 |
| 9 | Bit 0: PS, Bit 1: Touchpad-Klick, Bit 2: Mikrofon |
| 15–20 | Gyro X/Y/Z (int16 LE, roh) |
| 21–26 | Beschleunigung X/Y/Z (int16 LE, roh) |
| 32–35 / 36–39 | Touch-Punkt 1 / 2: Byte 0 Bit 7 = 1 heißt *nicht* berührt, X 12 Bit (0–1919), Y 12 Bit (0–1079) |
| 52 | Akku: Bits 0–3 Ladestand (0–10), Bits 4–7 Status (0 entlädt, 1 lädt, 2 voll) |

## Bewusst noch nicht in Schritt 1

- Gyro-Kalibrierung (Daten stecken in Feature-Report `0x05`, kommt später)

# Schritt 2 – Ausgaben

## Output-Report

| | USB | Bluetooth |
|---|---|---|
| Report-ID | `0x02` | `0x31` |
| Länge | 48 | 78 |
| Header | – | Byte 1 = Sequenz (oberes Nibble), Byte 2 = Tag `0x10` |
| Daten ab Byte | 1 | 3 |
| Prüfsumme | – | CRC32 über `0xA2` + Byte 0–73, Little Endian in Byte 74–77 |

Gemeinsamer 47-Byte-Block (Offset relativ zum Datenbeginn):

| Offset | Inhalt |
|---|---|
| 0 | valid_flag0: Bit 0/1 Rumble, Bit 2 rechter Trigger, Bit 3 linker Trigger |
| 1 | valid_flag1: Bit 0 Mikro-LED, Bit 2 Lightbar, Bit 4 Spieler-LEDs |
| 2 / 3 | Motor rechts (fein) / links (stark), 0–255 |
| 8 | Mikro-LED: 0 aus, 1 an, 2 pulsieren |
| 10–20 | Effekt rechter Trigger (11 Byte) |
| 21–31 | Effekt linker Trigger (11 Byte) |
| 38 | valid_flag2: Bit 0 LED-Helligkeit, Bit 1 Lightbar-Setup, Bit 2 Rumble (neuere Firmware) |
| 41 | Lightbar-Setup: `0x02` blendet das blaue Start-Licht aus (einmalig nötig) |
| 42 | Helligkeit Spieler-LEDs: 0 hell, 1 mittel, 2 dunkel |
| 43 | Spieler-LEDs, 5 Bit |
| 44–46 | Lightbar R, G, B |

## Trigger-Effekte

Der Trigger-Weg ist in 10 Zonen geteilt (0 = losgelassen, 9 = durchgedrückt).

| Effekt | Modus | Parameter |
|---|---|---|
| Aus | `0x05` | – |
| Widerstand / Abschnitt | `0x21` | Byte 1–2: aktive Zonen (Bitmaske), Byte 3–6: 3 Bit Stärke pro Zone |
| Waffe | `0x25` | Byte 1–2: Bit für Start- und Endzone, Byte 3: Stärke − 1 |
| Vibration | `0x26` | wie `0x21` (Amplitude statt Stärke), Byte 9: Frequenz in Hz |

Beim Schließen der App werden Rumble und Trigger zurückgesetzt, Lightbar und LEDs bleiben.
