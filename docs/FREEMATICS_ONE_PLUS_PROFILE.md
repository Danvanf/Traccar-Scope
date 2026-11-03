# Freematics ONE+ experimental profile

This bundled profile targets telelogger v5 messages decoded by Traccar's `freematics` protocol. It is based on source inspection on 2026-10-26, without device or vehicle validation. Firmware variants and older Traccar decoders may differ. It is not an endorsement of every ONE+ hardware variant.

## Setup and feedback

1. Configure compatible firmware to send to your Traccar server (default Freematics port 5170), register the device, and confirm positions arrive. See [Traccar configuration](https://www.traccar.org/freematics-configuration/).
2. Create or select the vehicle in Scope, bind its Traccar device, and assign **Freematics ONE+ (experimental; telelogger v5 / Traccar freematics)** in Vehicle Catalog. Newly bundled profiles become available with an updated app build; existing profile edits are preserved.
3. Choose fields in the vehicle status card or map popup preferences. A profile defines possible fields; it does not cause the tracker to collect or transmit them.
4. Use the existing device protocol profile editor to propose corrections. Open the profile submission dialog, select the device and a short sample date range, load points, review the proposed profile and included fields, and add the hardware model, firmware revision/configuration, Traccar version, and expected readings to the note.
5. Remove any location, VIN, device identifier, or other sample fields you do not want to share. Submit using **Send to support**, or download the ZIP using **Send it yourself** when app email is unavailable. Proposals require support review; sending does not publish or save an unsaved draft.

## Mapping evidence

Sources reviewed (upstream `master` branches, which may change):

- [Traccar Freematics decoder](https://github.com/traccar/traccar/blob/master/src/main/java/org/traccar/protocol/FreematicsProtocolDecoder.java): recognized packet keys, numeric parsing, scaling, and fallback names.
- [Traccar Position attributes](https://github.com/traccar/traccar/blob/master/src/main/java/org/traccar/model/Position.java): exact decoded attribute names.
- [Freematics telelogger v5](https://github.com/stanleyhuangyc/Freematics/blob/master/firmware_v5/telelogger/telelogger.ino): default OBD polling list, normalized PID serialization (`pid | 0x100`), battery voltage, and vector serialization.
- [Freematics OBD normalization](https://github.com/stanleyhuangyc/Freematics/blob/master/libraries/FreematicsPlus/FreematicsOBD.cpp): temperature, pressure, percentage, RPM, and timing values are normalized before transmission.

| Packet key (hex) | Traccar attribute | Interpretation used by Scope |
| --- | --- | --- |
| 104 | engineLoad | Already normalized percent |
| 105 | coolantTemp | Already normalized Celsius |
| 10C | rpm | Already normalized RPM |
| 10D | obdSpeed | Already normalized km/h |
| 111 | throttle | Already normalized percent |
| 10A | io266 | Already normalized kPa; do not multiply by 3 again |
| 10E | io270 | Already normalized timing degrees; retain negative values |
| 10F | io271 | Already normalized Celsius; do not subtract 40 again |
| 24 | battery | Decoder divides by 100 to produce volts; source is OBD/supply voltage, not tracker backup battery |
| 82 | deviceTemp | Decoder divides by 10; label warns to verify firmware scale |
| F | sat | Satellite count |
| 12 | hdop | Decoder stores integer unchanged; no assumed scale correction |
| 81 | rssi | Decoder stores integer unchanged; no assumed dBm, percent, or universal sentinel |
| 20 | acceleration | Decoder retains vector text; no scalar acceleration or driving-event mapping |
| VIN event | vin | Identifier text, including leading characters |

The generic fallback converts hex packet keys to decimal names: `0x10A = 266`, not `io10A`. Teltonika AVL IDs are unrelated and must not be reused here. Latitude, longitude, GNSS speed, course, altitude, and timestamps are top-level Traccar position fields and are handled by Scope's existing route pipeline.

## Known limitations and validation checklist

- No synthetic ignition or motion mapping: the reviewed decoder does not emit these fields directly. Confirm actual trip/ignition behavior with real records before claiming full trip detection support.
- No DTC count or code-string mapping: the reviewed decoder has no explicit DTC decoding case. A firmware library's ability to read DTCs does not establish that those codes reach Traccar.
- No promises for fuel level, MAF, odometer, or additional PIDs absent from the default polling list. Leave new observed keys unmapped until their source representation is verified.
- Verify device temperature against the exact firmware. The reviewed telelogger assigns integer sensor temperature while Traccar divides the received value by ten; a profile cannot establish that the two upstream versions agree. The profile preserves Traccar's decoded value without another correction.
- Confirm coolant warmup, stopped and moving OBD speed, idle/driving RPM, throttle, supply voltage, fuel pressure, and negative timing advance against an independent reading. Check missing/stale samples and GPS/OBD timing alignment.
- Check signal range/sentinels, HDOP scaling, and acceleration vector representation with real hardware before adding conversions or alerts.

Automated profile checks establish schema validity, unit handling, and separation from other manufacturers. They are synthetic examples, not evidence of device compatibility.
