# Third-party notices

PadPilot includes the following libraries. They are compiled into `PadPilot.exe`.

| Library | Version | License | Project |
|---|---|---|---|
| HidSharp | 2.1.0 | Apache License 2.0 | <https://www.zer7.com/software/hidsharp> |
| Nefarius.ViGEm.Client | 1.21.256 | MIT | <https://github.com/ViGEm/ViGEm.NET> |
| .NET 8 runtime (self-contained build) | 8.0 | MIT | <https://github.com/dotnet/runtime> |

The full license texts are available at the project links above and at
<https://www.apache.org/licenses/LICENSE-2.0> (Apache License 2.0) and
<https://opensource.org/license/mit> (MIT).

## Drivers (not included)

PadPilot can use these drivers if they are installed on the PC. They are separate projects
by their own authors and are not part of PadPilot:

- **ViGEmBus** – virtual Xbox 360 / DualShock 4 controllers. <https://github.com/nefarius/ViGEmBus>
- **HidHide** – hides the real controller from games while emulation is running. <https://github.com/nefarius/HidHide>

## Protocol documentation

The DualSense report layout used by PadPilot follows the publicly documented Linux kernel driver
`hid-playstation` and other open documentation. No code from other controller tools was copied.

## Trademarks

"PlayStation", "DualSense" and "DualShock" are trademarks of Sony Interactive Entertainment Inc.
"Xbox" is a trademark of Microsoft Corporation. PadPilot is an independent project and is not
affiliated with, endorsed or sponsored by Sony or Microsoft.
