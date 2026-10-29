# Feature-Vergleich: DualSenseX / DSX ↔ PadPilot

Stand: 28.10.2026. Grundlage sind nur die öffentlichen Feature-Beschreibungen
(dualsensex.com, GitHub-README von DualSenseX, Steam-Seite von DSX), nicht das Programm selbst.

Legende: ✅ vorhanden · 🗓️ im Fahrplan · ➕ Vorschlag für den Fahrplan · ❌ bewusst nicht geplant

## Controller und Verbindung

| Feature | DSX | PadPilot |
|---|---|---|
| DualSense über USB und Bluetooth | ✅ | ✅ |
| DualSense Edge | ✅ | ✅ erkannt, Zusatztasten (Fn, Rückseite) fehlen noch ➕ |
| DualShock 4 als echter Controller | ✅ (Steam) | ➕ |
| Akkustand | ✅ | ✅ (inkl. Ladestatus) |
| Tasten-/Stick-Tester | ✅ | ✅ inkl. Deadzones, Drift-Anzeige, Rundheitstest |
| Automatisch verbinden beim Start | ✅ | ✅ inkl. Hotplug |
| Option „Steam beenden“ beim Verbinden | ✅ | ➕ (Steam Input greift sonst evtl. auch auf den Controller zu) |

## Emulation

| Feature | DSX | PadPilot |
|---|---|---|
| Xbox-360-Emulation (ViGEmBus) | ✅ | ✅ inkl. Deadzones und Rumble aus Spielen |
| DualShock-4-Emulation (inkl. Touchpad/Gyro) | ✅ | ✅ Tasten, Sticks, Trigger, Rumble; Touchpad-Bewegung und Gyro ➕ |
| Echten Controller verstecken (HidHide) | ✅ optional | ✅ automatisch während der Emulation, Installation per Knopf |
| Keine Emulation (nur Trigger/LEDs) | ✅ | ✅ |
| Virtueller DualSense | ✅ (DSX+, kostenpflichtig) | ❌ (kein offener Treiber dafür) |

## Ausgaben

| Feature | DSX | PadPilot |
|---|---|---|
| Lightbar-Farbe | ✅ | ✅ |
| Lichtszenen / Effekte (Atmen, Regenbogen, Akku-Farbe …) | ✅ (Steam) | ➕ |
| Spieler-LEDs, Mikrofon-LED | ✅ | ✅ |
| Rumble | ✅ | ✅ |
| Adaptive Trigger mit Presets | ✅ | ✅ Widerstand, Abschnitt, Waffe, Vibration |
| Weitere Trigger-Modi (DSX nennt 17 eigene Modi) | ✅ | ➕ z. B. Galopp, Maschinengewehr, Bogen, mehrstufiger Widerstand |
| Audio über Lautsprecher/Kopfhörer am Controller (USB) | ✅ | ➕ später, Windows erkennt den DualSense per USB schon als Audiogerät |
| Audio-Haptik (Ton → Vibration, USB) | ✅ | ➕ später, aufwendig |
| Audio/Haptik über Bluetooth | ✅ (DSX+, kostenpflichtig) | ❌ (undokumentiert) |

## Eingabe-Zuordnung

| Feature | DSX | PadPilot |
|---|---|---|
| Touchpad als Maus | ✅ | ✅ Bewegen, Klicken (rechte Hälfte = Rechtsklick), Zwei-Finger-Scrollen, Antippen |
| Gyro als Maus | ✅ | ✅ mit Aktivierungstaste und Kalibrierung |
| Tasten neu belegen | ✅ | ➕ |
| Controller → Tastatur/Maus (für Spiele ohne Controller-Support) | ✅ | ➕ |
| Tastatur-Kürzel / Makros | ✅ (Shortcuts) | ➕ |

## Spiele-Anbindung

| Feature | DSX | PadPilot |
|---|---|---|
| UDP-Server für Mods (Trigger, LEDs) | ✅ | ✅ eigenes JSON-Protokoll (docs/MOD-PROTOKOLL.md), Status-Antwort; DSX-Format nicht kompatibel |
| Textdatei-Methode für Mods | ✅ | ✅ |
| Spiele-Liste / Workshop-Profile | ✅ | ❌ (bräuchte einen eigenen Server) |

## Programm

| Feature | DSX | PadPilot |
|---|---|---|
| Profile speichern | ✅ | 🗓️ Schritt 6 |
| Mit Windows starten | ✅ | 🗓️ Schritt 6 |
| In den Infobereich minimieren, minimiert starten | (nicht beschrieben) | ✅ |
| Mehrere Sprachen | ✅ 20+ | ✅ Deutsch, Englisch (weitere über .resx-Dateien) |
| Helles/dunkles Design, Hintergrundbilder, Rahmenfarbe | ✅ | ➕ |
| Selbst-Update | ✅ | ➕ (z. B. über GitHub-Releases) |
| Firmware-Prüfung | ✅ (Steam) | ❌ |
| Statistiken, Erfolge, Steam Cloud | ✅ (Steam) | ❌ |

## Empfehlung für die Reihenfolge

1. **Schritt 3** um DS4-Emulation und HidHide erweitern. Beides gehört direkt zur Emulation.
2. **Schritt 4** (Touchpad/Gyro-Maus) zusammen mit Tasten-Neubelegung und Controller → Tastatur/Maus.
3. **Lichteffekte** und **weitere Trigger-Modi**, beides kleine Erweiterungen von Schritt 2.
4. **Schritt 5** (UDP) plus Textdatei-Methode.
5. **Schritt 6** (Profile, Autostart).
6. Danach optional: Audio über den Controller, Audio-Haptik, Design-Optionen, Selbst-Update.
