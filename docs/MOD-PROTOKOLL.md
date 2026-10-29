# PadPilot Mod-Protokoll (Version 1)

Spiele-Mods können über PadPilot die Lightbar, die LEDs, das Rumble und die adaptiven Trigger
des DualSense steuern. PadPilot muss dafür laufen; die Einstellungen stehen im Tab **„Mods“**.

Es gibt zwei Wege:

| Weg | Wann | Ziel |
|---|---|---|
| **UDP** | Standard, schnell (jede Sekunde viele Befehle möglich) | `127.0.0.1`, Port **6970** (einstellbar) |
| **Textdatei** | Für Mods, die kein Netzwerk nutzen können | `%AppData%\PadPilot\mod-commands.json` (im Tab „Mods“ einschalten) |

Der Server nimmt nur Pakete von diesem PC an (localhost).

## Befehl

Ein Befehl ist ein JSON-Objekt. **Jedes Feld ist optional** – es ändert sich nur, was gesendet wird.
Groß-/Kleinschreibung der Feldnamen ist egal.

```json
{
  "lightbar": "#FF0000",
  "playerLeds": [0, 0, 1, 0, 0],
  "micLed": "pulse",
  "rumble": { "left": 200, "right": 50 },
  "leftTrigger": { "mode": "resistance", "start": 2, "strength": 4 },
  "rightTrigger": { "mode": "weapon", "start": 2, "end": 6, "strength": 8 }
}
```

| Feld | Werte |
|---|---|
| `lightbar` | `"#RRGGBB"` oder `{ "r": 0–255, "g": 0–255, "b": 0–255 }` |
| `playerLeds` | Bitmaske `0`–`31` oder Array mit 5 Werten (links → rechts, `0`/`1` oder `false`/`true`) |
| `micLed` | `"off"`, `"on"`, `"pulse"` |
| `rumble` | `{ "left": 0–255, "right": 0–255 }` – links stark, rechts fein |
| `leftTrigger`, `rightTrigger` | `"off"` oder ein Effekt-Objekt (siehe unten) |
| `reset` | `true` = alle bisherigen Mod-Einstellungen verwerfen, PadPilots eigene Einstellungen gelten wieder |

### Trigger-Effekte

Der Trigger-Weg ist in 10 Positionen geteilt: `0` = losgelassen, `9` = ganz durchgedrückt.
Stärke `1`–`8`. Fehlende Werte bekommen einen Standard.

| `mode` | Parameter | Wirkung |
|---|---|---|
| `off` | – | kein Effekt |
| `resistance` | `start`, `strength` | Widerstand ab `start` bis zum Ende |
| `section` | `start`, `end`, `strength` | Widerstand nur zwischen `start` und `end` |
| `weapon` | `start` (2–7), `end` (bis 8), `strength` | Widerstand baut sich auf und gibt nach – wie ein Abzug |
| `vibration` | `start`, `strength`, `frequency` (Hz) | Trigger vibriert ab `start` |

## Vorrang

Was eine Mod setzt, gilt **zusätzlich zu und vor** den Einstellungen im Tab „Ausgaben“ –
bis die Mod `{"reset": true}` schickt oder der Benutzer im Tab „Mods“ auf „Zurücksetzen“ klickt.
Spiel-Rumble aus der Emulation kommt obendrauf (der stärkere Wert gewinnt).

Tipp für Mods: beim Beenden des Spiels bzw. im Hauptmenü `{"reset": true}` senden.

## Antwort (nur UDP)

Jedes Paket wird an den Absender beantwortet:

```json
{"ok":true,"connected":true,"controller":"DualSense","connection":"bluetooth","battery":85,"charging":false,"emulation":"xbox360","modOverrides":true}
```

Bei einem Fehler: `{"ok":false,"error":"unknown trigger mode \"explode\" ..."}`.
Ein leeres Objekt `{}` ändert nichts und fragt nur den Status ab.

## Beispiele

**PowerShell**

```powershell
$udp = New-Object System.Net.Sockets.UdpClient
$msg = [Text.Encoding]::UTF8.GetBytes('{ "lightbar": "#00FF00", "rightTrigger": { "mode": "weapon", "start": 2, "end": 6, "strength": 8 } }')
[void]$udp.Send($msg, $msg.Length, "127.0.0.1", 6970)
```

**Python**

```python
import json, socket
s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.sendto(json.dumps({"rumble": {"left": 255, "right": 0}}).encode(), ("127.0.0.1", 6970))
print(s.recv(1024).decode())  # Status-Antwort
```

**C#**

```csharp
using var udp = new System.Net.Sockets.UdpClient();
byte[] msg = System.Text.Encoding.UTF8.GetBytes("""{ "leftTrigger": { "mode": "resistance", "start": 3, "strength": 6 } }""");
udp.Send(msg, msg.Length, "127.0.0.1", 6970);
```

**Textdatei:** denselben JSON-Text in `mod-commands.json` schreiben – PadPilot liest die Datei bei jeder Änderung neu.
