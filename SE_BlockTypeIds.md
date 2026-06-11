# Space Engineers — Complete Block TypeId Reference

Read from game files: `C:\Program Files (x86)\Steam\steamapps\common\SpaceEngineers\Content\Data\CubeBlocks\`

Last updated: AGM v1.5 session (June 2026)

---

## How Block TypeIds Work

In SE scripting, blocks are identified by their `BlockDefinition.TypeIdString` and `BlockDefinition.SubtypeId`.

- Non-functional blocks (armor, structural, interior) use TypeId `CubeBlock`
- Functional blocks use a specific TypeId like `Reactor`, `Assembler`, `JumpDrive` etc
- In scripts: `b.BlockDefinition.TypeIdString` returns e.g. `MyObjectBuilder_Reactor`
- For interface checks: cast to `IMyReactor`, `IMyAssembler` etc

---

## Armor — TypeId: `CubeBlock`

### Light Armor (CubeBlocks_Armor.sbc)
Block, Slope, Corner, CornerInv, Slope2Base, Slope2Tip, Corner2Base, Corner2Tip,
RoundSlope, RoundCorner, RoundCornerInv, HalfArmorBlock, HalfSlopeArmorBlock
- Large grid: prefix `LargeBlockArmor...`
- Small grid: prefix `SmallBlockArmor...`

### Heavy Armor
Same shapes as light armor
- Large grid: prefix `LargeHeavyBlockArmor...`
- Small grid: prefix `SmallHeavyBlockArmor...`

### Extended Armor Shapes (CubeBlocks_Armor_2.sbc)
CornerSquare, CornerSquareInverted, HalfCorner, HalfSlopeCorner, HalfSlopeCornerInverted,
HalfSlopedCorner, HalfSlopedCornerBase, HalfSlopeInverted, SlopedCorner, SlopedCornerBase, SlopedCornerTip

### Transition Armor (CubeBlocks_Armor_3.sbc)
RaisedSlopedCorner, SlopeTransition, SlopeTransitionBase, SlopeTransitionTip,
SlopeTransitionMirrored, SquareSlopedCornerBase, SquareSlopedCornerTip, SquareSlopedCornerTipInv

### Armor Panels (CubeBlocks_ArmorPanels.sbc)
Full Panel, Half Panel, Quarter Panel, Center Panel, Half Center Panel,
Sloped Panel, Half Sloped Panel, Sloped Side Panel, 2x1 Sloped Panel variants,
Round Panel, Round Corner Panel, Round Face Panel
- Light: suffix `...Light`
- Heavy: suffix `...Heavy`

---

## Interior Blocks — TypeId: `CubeBlock`

| SubtypeId | Description |
|---|---|
| LargeBlockInteriorWall | Interior wall |
| LargeCoverWall | Cover wall |
| LargeCoverWallHalf | Half cover wall |
| LargeInteriorPillar | Interior pillar |
| LargeRamp | Ramp |
| LargeStairs | Stairs |
| LargeSteelCatwalk | Catwalk straight |
| LargeSteelCatwalk2Sides | Catwalk 2-sided |
| LargeSteelCatwalkCorner | Catwalk corner |
| LargeSteelCatwalkPlate | Catwalk plate |
| Passage2 | Passage |
| Passage2Wall | Passage wall |
| AirDuct1 | Air duct straight |
| AirDuct2 | Air duct |
| AirDuctCorner | Air duct corner |
| AirDuctGrate | Air duct grate |
| AirDuctRamp | Air duct ramp |
| AirDuctT | Air duct T |
| AirDuctX | Air duct X |

### Interior Seats — TypeId: `Cockpit`
| SubtypeId | Description |
|---|---|
| PassengerSeatLarge | Large passenger seat |
| PassengerSeatSmall | Small passenger seat |
| PassengerSeatSmallNew | Small passenger seat (new) |
| PassengerSeatSmallOffset | Small passenger seat (offset) |

### Ladders — TypeId: `Ladder2`
| SubtypeId | Description |
|---|---|
| LadderShaft | Ladder shaft |
| LadderSmall | Small ladder |

---

## Structural — TypeId: `CubeBlock`

| SubtypeId | Description |
|---|---|
| LargeBlockStructural_Frame | Structural frame |
| LargeBlockStructural_Platform | Structural platform |
| LargeBlockStructural_PlatformTriangle | Triangular platform |
| LargeBlockStructural_SupportBeam3x3 | Support beam 3x3 |
| LargeBlockStructural_SupportBeam4x4 | Support beam 4x4 |
| LargeBlockStructural_SupportBeam5x5 | Support beam 5x5 |

---

## Power

### Reactors — TypeId: `Reactor`
| SubtypeId | Grid | Description |
|---|---|---|
| LargeBlockLargeGenerator | Large | Large reactor |
| LargeBlockSmallGenerator | Large | Small reactor |
| SmallBlockLargeGenerator | Small | Large reactor |
| SmallBlockSmallGenerator | Small | Small reactor |
| LargeBlockLargeGeneratorWarfare2 | Large | Warfare large reactor |
| LargeBlockSmallGeneratorWarfare2 | Large | Warfare small reactor |
| SmallBlockLargeGeneratorWarfare2 | Small | Warfare large reactor |
| SmallBlockSmallGeneratorWarfare2 | Small | Warfare small reactor |

### Batteries — TypeId: `BatteryBlock`
| SubtypeId | Grid |
|---|---|
| LargeBlockBatteryBlock | Large |
| SmallBlockBatteryBlock | Small |
| SmallBlockSmallBatteryBlock | Small |
| LargeBlockBatteryBlockWarfare2 | Large |
| SmallBlockBatteryBlockWarfare2 | Small |
| LargeBlockPrototechBattery | Large |
| SmallBlockPrototechBattery | Small |

### Solar Panels — TypeId: `SolarPanel`
| SubtypeId | Grid |
|---|---|
| LargeBlockSolarPanel | Large |
| SmallBlockSolarPanel | Small |

### Wind Turbines — TypeId: `WindTurbine`
| SubtypeId | Grid |
|---|---|
| LargeBlockWindTurbine | Large |

### Hydrogen Engines — TypeId: `HydrogenEngine`
| SubtypeId | Grid |
|---|---|
| LargeHydrogenEngine | Large |
| SmallHydrogenEngine | Small |
| LargePrototechReactor | Large |

---

## Propulsion

### Thrusters — TypeId: `Thrust`
| SubtypeId | Type |
|---|---|
| LargeBlockLargeThrust | Large grid — ion large |
| LargeBlockSmallThrust | Large grid — ion small |
| SmallBlockLargeThrust | Small grid — ion large |
| SmallBlockSmallThrust | Small grid — ion small |
| LargeBlockLargeAtmosphericThrust | Large grid — atmo large |
| LargeBlockSmallAtmosphericThrust | Large grid — atmo small |
| SmallBlockLargeAtmosphericThrust | Small grid — atmo large |
| SmallBlockSmallAtmosphericThrust | Small grid — atmo small |
| LargeBlockLargeHydrogenThrust | Large grid — hydro large |
| LargeBlockSmallHydrogenThrust | Large grid — hydro small |
| SmallBlockLargeHydrogenThrust | Small grid — hydro large |
| SmallBlockSmallHydrogenThrust | Small grid — hydro small |
| LargeBlockLargeModularThruster | Large grid — modular large |
| LargeBlockSmallModularThruster | Large grid — modular small |
| SmallBlockLargeModularThruster | Small grid — modular large |
| SmallBlockSmallModularThruster | Small grid — modular small |
| LargeBlockPrototechThruster | Large — Prototech |
| SmallBlockPrototechThruster | Small — Prototech |
Flat and DShape variants also exist for atmospheric thrusters.

### Gyroscopes — TypeId: `Gyro`
| SubtypeId | Grid |
|---|---|
| LargeBlockGyro | Large |
| SmallBlockGyro | Small |
| LargeBlockPrototechGyro | Large |
| SmallBlockPrototechGyro | Small |

### Jump Drives — TypeId: `JumpDrive`
| SubtypeId | Grid |
|---|---|
| LargeJumpDrive | Large |
| LargePrototechJumpDrive | Large |
| SmallPrototechJumpDrive | Small |

---

## Production

### Assemblers — TypeId: `Assembler`
| SubtypeId | Description |
|---|---|
| BasicAssembler | Basic assembler |
| LargeAssembler | Advanced assembler |
| LargePrototechAssembler | Prototech assembler |
| FoodProcessor | Food processor (Apex Survival) |

### Refineries — TypeId: `Refinery`
| SubtypeId | Description |
|---|---|
| LargeRefinery | Standard refinery |
| Blast Furnace | Blast furnace |
| LargePrototechRefinery | Prototech refinery |
| SmallPrototechRefinery | Small Prototech refinery |

### Upgrade Modules — TypeId: `UpgradeModule`
| SubtypeId | Description |
|---|---|
| LargeProductivityModule | Productivity |
| LargeEffectivenessModule | Effectiveness |
| LargeEnergyModule | Energy |

### Survival Kit — TypeId: `SurvivalKit`
| SubtypeId |
|---|
| SurvivalKit |
| SurvivalKitLarge |

---

## Control

### Cockpits — TypeId: `Cockpit`
| SubtypeId | Description |
|---|---|
| LargeBlockCockpit | Large cockpit |
| LargeBlockCockpitSeat | Seat cockpit |
| LargeBlockStandingCockpit | Standing cockpit |
| SmallBlockCockpit | Small cockpit |
| SmallBlockFlushCockpit | Flush cockpit |
| SmallBlockStandingCockpit | Small standing cockpit |
| OpenCockpitLarge | Open cockpit large |
| OpenCockpitSmall | Open cockpit small |
| CockpitOpen | Open cockpit |
| DBSmallBlockFighterCockpit | Fighter cockpit |
| RoverCockpit | Rover cockpit |
| PassengerBench | Passenger bench |
| LargeBlockLabDeskSeat | Lab desk seat |

### Remote Controls — TypeId: `RemoteControl`
| SubtypeId |
|---|
| LargeBlockRemoteControl |
| SmallBlockRemoteControl |

### Programmable Blocks — TypeId: `MyProgrammableBlock`
| SubtypeId |
|---|
| LargeProgrammableBlock |
| SmallProgrammableBlock |

### Timer Blocks — TypeId: `TimerBlock`
| SubtypeId |
|---|
| TimerBlockLarge |
| TimerBlockSmall |

### Event Controllers — TypeId: `EventControllerBlock`
| SubtypeId |
|---|
| EventControllerLarge |
| EventControllerSmall |

### Button Panels — TypeId: `ButtonPanel`
| SubtypeId |
|---|
| ButtonPanelLarge |
| ButtonPanelSmall |
| LargeButtonPanelPedestal |
| SmallButtonPanelPedestal |

### Sensor Blocks — TypeId: `SensorBlock`
| SubtypeId |
|---|
| LargeBlockSensor |
| SmallBlockSensor |

---

## Cargo and Logistics

### Cargo Containers — TypeId: `CargoContainer`
| SubtypeId | Description |
|---|---|
| LargeBlockLargeContainer | Large cargo |
| LargeBlockSmallContainer | Small cargo (large grid) |
| SmallBlockLargeContainer | Large cargo (small grid) |
| SmallBlockMediumContainer | Medium cargo |
| SmallBlockSmallContainer | Small cargo |
| LargeBlockCargoTerminal | Cargo terminal |
| LargeBlockCargoTerminalHalf | Half cargo terminal |
| LargeBlockLabCabinet | Lab cabinet |
| LargeBlockLabCornerDesk | Lab corner desk |
| LargeBlockWeaponRack | Weapon rack |
| SmallBlockWeaponRack | Small weapon rack |

### Conveyors — TypeId: `ConveyorSorter`
| SubtypeId |
|---|
| LargeBlockConveyorSorter |
| MediumBlockConveyorSorter |
| SmallBlockConveyorSorter |

### Collectors — TypeId: `Collector`
| SubtypeId |
|---|
| Collector |
| CollectorSmall |

### Connectors — TypeId: `ShipConnector`
| SubtypeId | Description |
|---|---|
| Connector | Large connector |
| ConnectorMedium | Medium connector |
| ConnectorSmall | Small connector |
| LargeBlockInsetConnector | Inset connector |
| LargeBlockInsetConnectorSmall | Small inset connector |
| SmallBlockInsetConnector | Small grid inset connector |
| SmallBlockInsetConnectorMedium | Small grid medium inset |
| LargeBlockStructural_PlatformConnector | Structural platform connector |

---

## Weapons

### Fixed Weapons — TypeId: `SmallGatlingGun` / `SmallMissileLauncher` / `SmallMissileLauncherReload`
| SubtypeId | TypeId | Description |
|---|---|---|
| SmallGatlingGunWarfare2 | SmallGatlingGun | Gatling gun |
| SmallMissileLauncherWarfare2 | SmallMissileLauncher | Missile launcher |
| SmallRocketLauncherReload | SmallMissileLauncherReload | Reloadable rocket launcher |

### Turrets — TypeId: `LargeTurretBase`
| SubtypeId | Description |
|---|---|
| SmallGatlingTurret | Gatling turret |
| SmallMissileTurret | Missile turret |
| LargeInteriorTurret | Interior turret |
| AutoCannonTurret | Autocannon turret |
| LargeBlockMediumCalibreTurret | Medium calibre turret |
| LargeCalibreTurret | Large calibre turret |
| SmallBlockMediumCalibreTurret | Small grid medium calibre |

### Fixed Railguns — TypeId: `WeaponBlock`
| SubtypeId | Description |
|---|---|
| LargeRailgun | Large railgun |
| SmallRailgun | Small railgun |
| LargeBlockLargeCalibreGun | Large calibre gun |
| SmallBlockAutocannon | Autocannon |
| SmallBlockMediumCalibreGun | Medium calibre gun |
| LargeMissileLauncher | Large missile launcher |
| LargeFlareLauncher | Large flare launcher |
| SmallFlareLauncher | Small flare launcher |

### Warheads — TypeId: `Warhead`
| SubtypeId |
|---|
| LargeWarhead |
| SmallWarhead |

### Turret Control — TypeId: `TurretControlBlock`
| SubtypeId |
|---|
| LargeTurretControlBlock |
| SmallTurretControlBlock |

### Decoys — TypeId: `Decoy`
| SubtypeId |
|---|
| LargeDecoy |
| SmallDecoy |

---

## Medical

### Medical Rooms — TypeId: `MedicalRoom`
| SubtypeId | Description |
|---|---|
| LargeMedicalRoom | Medical room |
| LargeRefillStation | Large refill station |
| InsetRefillStation | Inset refill station |
| SmallRefillStation | Small refill station |

### Cryo Chambers — TypeId: `CryoChamber`
| SubtypeId |
|---|
| LargeBlockCryoChamber |
| SmallBlockCryoChamber |
| LargeBlockBedFree |
| LargeBlockCryoLabVat |

---

## Gas and Fuel

### Gas Tanks — TypeId: `GasTank`
| SubtypeId | Type |
|---|---|
| LargeHydrogenTank | Hydrogen — large |
| LargeHydrogenTankSmall | Hydrogen — small (large grid) |
| SmallHydrogenTank | Hydrogen — large (small grid) |
| SmallHydrogenTankSmall | Hydrogen — small (small grid) |
| OxygenTankSmall | Oxygen (large grid) |
| SmallOxygenTankSmall | Oxygen (small grid) |
| LargeBlockOxygenTankLab | Oxygen lab tank |
| LargeHydrogenTankSmallLab | Hydrogen lab tank |
| SmallHydrogenTankLab | Hydrogen lab (small grid) |

### Oxygen Generators (Ice) — TypeId: `OxygenGenerator`
| SubtypeId | Description |
|---|---|
| OxygenGeneratorSmall | Standard O2/H2 generator |
| LargeBlockOxygenGeneratorLab | Lab O2/H2 generator |
| SmallBlockOxygenGeneratorLab | Small lab generator |
| IrrigationSystem | Irrigation system (Apex Survival) |

### Oxygen Farms — TypeId: `OxygenFarm`
| SubtypeId |
|---|
| LargeBlockOxygenFarm |

### Air Vents — TypeId: `AirVent`
| SubtypeId |
|---|
| AirVentFull |
| SmallAirVent |
| SmallAirVentFull |

### Algae Farm / Farm Plot — TypeId: `FunctionalBlock`
| SubtypeId | Description |
|---|---|
| LargeBlockAlgaeFarm | Algae farm (Apex Survival) |
| LargeBlockFarmPlot | Farm plot (Apex Survival) |

---

## Communications

### Radio Antennas — TypeId: `RadioAntenna`
| SubtypeId |
|---|
| LargeBlockRadioAntenna |
| LargeBlockCompactRadioAntenna |
| SmallBlockRadioAntenna |

### Laser Antennas — TypeId: `LaserAntenna`
| SubtypeId |
|---|
| LargeBlockLaserAntenna |
| SmallBlockLaserAntenna |

### Beacons — TypeId: `Beacon`
| SubtypeId |
|---|
| LargeBlockBeacon |
| SmallBlockBeacon |

### Broadcast Controllers — TypeId: `BroadcastController`
| SubtypeId |
|---|
| LargeBlockBroadcastController |
| SmallBlockBroadcastController |

### Transponders — TypeId: `TransponderBlock`
| SubtypeId |
|---|
| LargeBlockTransponder |
| SmallBlockTransponder |

---

## Mechanical

### Pistons — TypeId: `PistonBase` / `ExtendedPistonBase`
| SubtypeId |
|---|
| LargePistonBase |
| SmallPistonBase |

### Rotors — TypeId: `MotorStator`
| SubtypeId |
|---|
| LargeStator |
| SmallStator |
| SmallAdvancedStator |
| SmallAdvancedStatorSmall |

### Hinges — TypeId: `MotorAdvancedStator`
| SubtypeId |
|---|
| LargeHinge |
| MediumHinge |
| SmallHinge |
| LargeAdvancedStator |

### Wheels — TypeId: `MotorSuspension`
Suspension1x1, Suspension2x2, Suspension3x3, Suspension5x5 (Large grid)
SmallSuspension1x1, etc (Small grid)
ShortSuspension variants for each size
Mirrored variants for each

### Merge Blocks — TypeId: `MergeBlock`
| SubtypeId |
|---|
| LargeShipMergeBlock |
| SmallShipMergeBlock |
| SmallShipSmallMergeBlock |

### Landing Gear — TypeId: `LandingGear`
| SubtypeId |
|---|
| LargeBlockLandingGear |
| LargeBlockSmallMagneticPlate |
| SmallBlockLandingGear |
| SmallBlockSmallMagneticPlate |

---

## Displays

### LCD Panels — TypeId: `TextPanel`
| SubtypeId | Description |
|---|---|
| LargeLCDPanel | Large LCD |
| LargeLCDPanelWide | Wide LCD |
| LargeTextPanel | Text panel |
| SmallLCDPanel | Small LCD |
| SmallLCDPanelWide | Small wide LCD |
| SmallTextPanel | Small text panel |
| LargeBlockCorner_LCD_1 | Corner LCD 1 |
| LargeBlockCorner_LCD_2 | Corner LCD 2 |
| LargeBlockCorner_LCD_Flat_1 | Flat corner LCD 1 |
| LargeBlockCorner_LCD_Flat_2 | Flat corner LCD 2 |
| SmallBlockCorner_LCD_1 | Small corner LCD 1 |
| SmallBlockCorner_LCD_2 | Small corner LCD 2 |
| SmallBlockCorner_LCD_Flat_1 | Small flat corner LCD 1 |
| SmallBlockCorner_LCD_Flat_2 | Small flat corner LCD 2 |

### Lights — TypeId: `InteriorLight` / `ReflectorLight`
| SubtypeId | TypeId |
|---|---|
| SmallLight | InteriorLight |
| SmallBlockSmallLight | InteriorLight |
| LargeBlockLight_1corner | InteriorLight |
| LargeBlockLight_2corner | InteriorLight |
| SmallBlockLight_1corner | InteriorLight |
| SmallBlockLight_2corner | InteriorLight |
| LargeLightPanel | InteriorLight |
| SmallLightPanel | InteriorLight |
| LargeBlockFrontLight | ReflectorLight |
| SmallBlockFrontLight | ReflectorLight |

### Searchlights — TypeId: `Searchlight`
| SubtypeId |
|---|
| LargeSearchlight |
| SmallSearchlight |

---

## Gravity

### Gravity Generators — TypeId: `GravityGenerator`
Standard and spherical variants

### Artificial Mass — TypeId: `VirtualMass`
| SubtypeId |
|---|
| VirtualMassLarge |
| VirtualMassSmall |

### Space Ball — TypeId: `SpaceBall`
| SubtypeId |
|---|
| SpaceBallLarge |
| SpaceBallSmall |

---

## Cameras and Detection

### Cameras — TypeId: `CameraBlock`
| SubtypeId |
|---|
| LargeCameraBlock |
| SmallCameraBlock |

### Ore Detectors — TypeId: `OreDetector`
| SubtypeId |
|---|
| LargeOreDetector |
| SmallBlockOreDetector |

### Projectors — TypeId: `Projector`
| SubtypeId |
|---|
| LargeProjector |
| SmallProjector |

---

## Tools (Ship)

### Drills — TypeId: `Drill`
| SubtypeId |
|---|
| LargeBlockDrill |
| SmallBlockDrill |
| LargeBlockPrototechDrill |

### Welders — TypeId: `ShipWelder`
| SubtypeId |
|---|
| LargeShipWelder |
| SmallShipWelder |

### Grinders — TypeId: `ShipGrinder`
| SubtypeId |
|---|
| LargeShipGrinder |
| SmallShipGrinder |

---

## Parachutes — TypeId: `Parachute`
| SubtypeId |
|---|
| LgParachute |
| SmParachute |

---

## Sound — TypeId: `SoundBlock`
| SubtypeId |
|---|
| LargeBlockSoundBlock |
| SmallBlockSoundBlock |

---

## AGM Interface Types

These are the C# interfaces AGM uses to check block types in scripting:

| Interface | Block type |
|---|---|
| `IMyReactor` | Reactors |
| `IMyBatteryBlock` | Batteries |
| `IMySolarPanel` | Solar panels |
| `IMyPowerProducer` | All power producers |
| `IMyAssembler` | Assemblers + Food Processor |
| `IMyRefinery` | Refineries + Blast Furnace |
| `IMyGasTank` | Gas tanks (H2 and O2) |
| `IMyGasGenerator` | O2/H2 generators + Irrigation |
| `IMyCargoContainer` | Cargo containers |
| `IMyShipConnector` | Connectors |
| `IMyLargeTurretBase` | All turrets |
| `IMyUserControllableGun` | Fixed weapons |
| `IMySmallGatlingGun` | Gatling gun |
| `IMySmallMissileLauncher` | Missile launcher |
| `IMyTextSurface` | Any surface that can draw |
| `IMyTextSurfaceProvider` | Blocks with screens (LCD, cockpit, PB) |
| `IMyLightingBlock` | Lights + corner LCDs |
| `IMyAirVent` | Air vents |
| `IMyProgrammableBlock` | Programmable blocks |
| `IMyShipDrill` | Drills |
| `IMyShipWelder` | Welders |
| `IMyShipGrinder` | Grinders |
| `IMyCockpit` | Cockpits + seats |
| `IMyMotorStator` | Rotors |
| `IMyPistonBase` | Pistons |
| `IMyMechanicalConnectionBlock` | Rotors + pistons + hinges |
| `IMyJumpDrive` | Jump drives |
| `IMyGyro` | Gyroscopes |
| `IMyThrust` | Thrusters |
| `IMyLandingGear` | Landing gear + magnetic plates |
| `IMySensorBlock` | Sensor blocks |
| `IMyTimerBlock` | Timer blocks |
| `IMyProjector` | Projectors |
