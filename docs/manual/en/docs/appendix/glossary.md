# Glossary

**Permanent mode**
: A setting in which the M-Logger starts measuring automatically when powered on and sends the readings to the PC (Zigbee). Even if the power goes off (for example when replacing the batteries), measurement restarts when the power is turned on again. Cancel it by holding the Reset button for 3 seconds or more ([Basic device operation](../start/device.md)).

**Destination**
: Where the readings are sent or saved: the smartphone (Bluetooth), the PC (Zigbee) or the built-in memory of the device. Chosen in [Measurement settings](../mobile/settings.md) of the smartphone app.

**Coordinator / end device**
: Terms used when collecting readings over Zigbee. The XBee on the PC is the coordinator (parent), and each M-Logger is an end device (child).

**XBee**
: The radio module used in the M-Logger and the coordinator. It handles Zigbee and Bluetooth (BLE) communication.

**PAN ID**
: The number of a Zigbee network. Only devices with the same PAN ID communicate ([Preparing the Zigbee coordinator](../server/coordinator.md)).

**Hardware ID**
: The serial number of the M-Logger device (8 alphanumeric characters). Used to look up the factory inspection report on the website.

**Dry-bulb temperature**
: The temperature of the air, commonly called "air temperature".

**Globe temperature**
: The temperature at the center of a black sphere (globe). It includes the effect of radiation from surrounding walls, windows, etc.

**Mean radiant temperature (MRT)**
: The temperature of a uniform surrounding surface that would give the same radiant exchange as the actual surroundings. Calculated from the globe temperature, dry-bulb temperature and air velocity.

**PMV / PPD**
: Thermal comfort indices. PMV predicts the thermal sensation on a scale from -3 (cold) to +3 (hot); PPD predicts the percentage of people dissatisfied with the environment [%].

**SET\***
: Standard new effective temperature. Expresses the combined effect of temperature, humidity, air velocity, radiation, clothing and metabolic rate as one temperature [°C].

**WBGT**
: Wet-bulb globe temperature, an index of heat stress used as a guide to the risk of heatstroke [°C]. There are formulas for indoor and outdoor use.

**Clothing insulation (clo) / metabolic rate (met)**
: The warmth of the clothing and the intensity of the activity, used to calculate PMV and other indices.
