ROHR

#### Program System for Static and Dynamic Analysis of Complex Piping

#### and Skeletal Structures

SINETZSINETZfluid

#### Steady State Calculation of Flow Distribution, Pressure Drop and

#### Heat Loss in Branched and Intermeshed Piping Networks

Interface 01.

Neutral Interface ROHR

Neutral Interface SINETZ

### SIGMA Ingenieurgesellschaft mbH


[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

```
Contents of this document are subject to change without notice. The manual is protected by copyright.
No part of this document may be reproduced or transmitted in any form or by any means, electronic or
mechanical, for any purpose, without permission.
```
```
Specifications subject to change without notice.
All of the mentioned products and brand names are trademarks or indexed trademarks of the respective
manufacturers.
```
```
Published by
```
```
SIGMA Ingenieurgesellschaft mbH
Bertha-von-Suttner-Allee 19
D-59423 Unna
Germany
Telephone +49 (0)2303 332 33- 0
Telefax +49 (0)2303 332 33- 50
E-mail info@rohr2.de
Internet httpwww.rohr2.de httpwww.rohr2.com
```

## ROHR2 SINETZ Neutral Interface Page i

_Content_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)


Page ii ROHR2 SINETZ Neutral Interface

_Contents_

- 1 Introduction Contents
- 2 Program task and solution
- 3 Interface handling
- 4 General requirements
- 5 Record types
- 5.1 General record types
   - 5.1.1 GEN General settings
   - 5.1.2 AUFT Project name
   - 5.1.3 TEXT User defined text.................................................................................................................................
   - 5.1.4 KN Node description
   - 5.1.5 LAST Load definitions
   - 5.1.6 DN Nominal width definition
   - 5.1.7 IS Insulation characteristics
- 5.2 Record types for elements
   - 5.2.1 Buried pipes
   - 5.2.2 RO Straight pipe
   - 5.2.3 PROF Profile, structural section
   - 5.2.4 BOG pipe bend
   - 5.2.5 TEV Branch of a reinforced tee
   - 5.2.6 TEW Weldolet
   - 5.2.7 TEE Tee........................................................................................................................................................
   - 5.2.8 RED Reducer
   - 5.2.9 FLA Flange
   - 5.2.10 FLABL Blind flange
   - 5.2.11 ARM Instrument
   - 5.2.12 ARMECK Angle valve
   - 5.2.13 ARM3W 3-way-valve
   - 5.2.14 ARM4W 4-way-valve
   - 5.2.15 KAX Axial expansion joint
   - 5.2.16 KANG Angular expansion joint
   - 5.2.17 BOX Any element with 2 connections
   - 5.2.18 KLAT Lateral expansion joint
   - 5.2.19 BODEN Head, Bottom
- 5.3 Input records for supports
   - 5.3.1 Hangers
      - 5.3.1.1 SH Rigid hanger
      - 5.3.1.2 FH Spring hanger
      - 5.3.1.3 KH Constant hanger
   - 5.3.2 Rigid supports
      - 5.3.2.1 ST General rigid support
      - 5.3.2.2 Definition of support types
   - 5.3.3 Spring supports
      - 5.3.3.1 FS general spring support
      - 5.3.3.2 Definition of support types
   - 5.3.4 Constant supports
      - 5.3.4.1 Definition of support types
   - 5.3.5 Angulating supports httpwww.rohr2.com SIGMA Ingenieurgesellschaft mbH
      - 5.3.5.1 GS General Angulating support
      - 5.3.5.2 Definition of support types of angulating rigid supports
      - 5.3.5.3 Definition of support types of angulating spring supports
      - 5.3.5.4 Definition of support types of constant angulating supports
   - 5.3.6 NOZZLE Nozzle
- 5.4 Other definitions at nodes - input records
   - 5.4.1 ADD_RES additional results
   - 5.4.2 PMASS Point mass
   - 5.4.3 IF internal spring
- 6 Configuration file ntr.env
- 6.1 Format of the file ntr.env
- 6.2 Defined sections
   - 6.2.1 Default - definition of the settings
   - 6.2.2 Material - conversion table for material names
- 7 Example Neutral Interface


ROHR2 SINETZ Neutral Interface Page 1

_Introduction_

_General record types_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

## Interface 01.01 Neutral Interface ROHR2SINETZ NTR

## 1 Introduction

Neutral interface for data exchange between

- the program system ROHR2 and optional available ROHR2 Interfaces
- the program system SINETZ and optional available SINETZ Interfaces

and for the data exchange between ROHR2SINETZ and various CAD-systems like

- AVEVA E3D PDMS
- INTERGRAPH - SMARTPLANT
- CADISON
- HiCADnext
- ROHR2CAD  RC-PLANET

The Neutral File interface is part of the ROHR2 Static and Dynamic standard program and SINETZ
standard program.

## 2 Program task and solution

To simplify the data interchange with CAD systems, the format of the neutral interface has been defined.
Being based on the listing of all elements in the system (pipe, bend, instruments, supports, ...) it can be
created e.g. by a report from a database.
For each element parameters required by ROHR2SINETZ have to be indicated.
In SINETZ only flow relevant parameters are considered.

_Neutral Interface – data import_

_ROHR2SINETZ_

It does not matter if parameters are missing only a part of the values must be entered by the user, not
indicated parameters are filled with standard values or calculated from other parameters.
The elements are written into an ASCII file as data sets with defined record label and corresponding
parameters.

_Neutral Interface – data export ROHR_

_ROHR_

The data transfer in ROHR2 Neutral file format (.ntr) is carried out by the ROHR2 function _File Export
Neutral topic._

_SINETZ_

In SINETZ the data export in Neutral file format (.ntr) is not possible.


Page 2 ROHR2 SINETZ Neutral Interface

_Interface_ handling

_General record types_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

## 3 Interface handling

_Requirements of the data transfer_

- a current program license ROHR
- the ROHR2 Neutral Interface

_Reading files in ROHR2 - Neutral Interface_

Read NTR-files by the _File Open_ command
Choose file type NTR (.ntr).
Several files can be selected in the dialog window _file-ope_ n. The data of all files is taken over into the
calculation system.

For details of the processing of NTR data please refer to the ROHR2SINETZ manual.

## 4 General requirements

- Record labels and parameters must be written in capital characters
- Record labels start in column 0
- Parameters have the format PARAM=WERT, without blank character between PARAM and =
- Parameters are separated by at least one blank character
- Text input is limited by ’
- Since the entire number of parameters is not always available, only the required parameters of
    the records have to be indicated. Not indicated parameters are filled with standard values of
    calculated from other parameters.
- Parameters may be in arbitrary order.
- Input records can be in arbitrary order.
- Coordinates may be entered by node names or directly. New node names are created if the
    coordinates are indicated directly.
    The maximum length of node names is 4 characters.
- Dimensions, types of insulation and loads are assigned to the elements by the name.
- Allowed material names are all the names given in the ROHR2 material file (see ROHR2basic-
    manual).
- Expected file extension is .NTR (.ntr)


ROHR2 SINETZ Neutral Interface Page 3

_Record_ types

_General record types_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

## 5 Record types

## 5.1 General record types

### 5.1.1 GEN General settings

_Structure_

```
GEN TMONT EB UNITKT FANG CODE
```
TMONT Assembly temperature in °C, used for the calculation of thermal expansion
Default 20

EB  Direction of acceleration due to gravity.
Possible input +X, -X, +Y,-Y,+Z. -Z.
Default Z

UNITKT Unit of coordinates.
Possible input M, MM
Default  M

FANG Grid in mm to get the coordinates (e.g. to ignore missing seals). It is looked for
connections between elements always in the grid distance. Default3 mm

CODE Calculation rule the following inputs are permissible
CL1ASME Class 1
CL2ASME Class 2
CL3ASME Class 3
B31.1ASME B31.
B31.3ASME B31.
B31.4ASME B31.
B31.5ASME B31.
B31.8ASME B31.
FDBR
AGFW, AGFW
KTA KTA 3201.
K3211.A1 KTA 3211.2 A
K3211.A2 KTA 3211.2 A2A
EN
KRV
WAVISTRONG
BS 7159 (British Standard)
ISO


Page 4 ROHR2 SINETZ Neutral Interface

_Record_ types

_General record types_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.1.2 AUFT Project name

_Structure_

```
AUFT TEXT
```
TEXT User defined text for project description

Only one AUFT-record is permitted.

### 5.1.3 TEXT User defined text.................................................................................................................................

_Structure_

```
TEXT TEXT
```
TEXT  User defined text

Maximum two TEXT-records are permitted.

### 5.1.4 KN Node description

Nodes are defined for the assignment of coordinates.
Definition of nodes is only necessary if specific node names should be indicated. Coordinates may be
inserted directly in the element records and boundary condition records.

_Structure_

```
KN NAME BNAME X Y Z TEXT
```
NAME Name of the node, max. 4 characters (required)
BNAME Name of the referring node
X, Y, Z Coordinates of the node. If a referring node (BNAME) is indicated, the coordinates, are
understood as difference coordinates to BNAME, otherwise absolute coordinates. At least
one direction of coordinates is required, not indicated directions are set to 0. Parameter
UNITKT in GEN-record defines the unit of coordinates (m or mm).
TEXT Arbitrary description of the node


ROHR2 SINETZ Neutral Interface Page 5

_Record_ types

_General record types_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.1.5 LAST Load definitions

Design- and operation data are indicated. The data is assigned to the elements by the names. Loads with
different parameters must have different names.

_Structure_

```
LAST NAME PA PB TA TB GAMMED
```
NAME Name of the load (required) max. 127 characters
PA Design pressure in bar, default value see _ntr.env_. If only PB is indicated, PA=PB is set
PB Operation pressure in bar. If PB is not indicated, PB=PA is set.
TA Design temperature in °C, default value see _ntr.env_. If only TB is indicated, TA=TB is set.
TB Operation temperature in °C. If TB is not indicated, TB=TA is set
GAMMED Medium density in kgm³, default value see _ntr.env_

### 5.1.6 DN Nominal width definition

The dimensions of the pipe elements are assigned by the definition of nominal widths. Nominal widths
with different parameters must have different names.
For the insulation at this point only the insulation thickness is required, further parameters of the insulation
are defined as an insulation type in the IS-record and assigned by the records name.

_Structure_

```
DN NAME DA S TOLI TOLA ISOTYP ISODICKE SM SMISO NORM
```
NAME  Name of the nominal width (required) max. 127 characters
DA  Outer diameter of pipe in mm (required)
S  Wall thickness of pipe in mm, default value see _ntr.env_
TOLA  Wall thickness tolerance outside in %, default value 0
TOLI  Wall thickness tolerance internal in %, default value 0
ISOTYP Name insulation type, has to be defined by the IS-record. ISOTYP has to be indicated
only if the insulation thickness  0mm (ISODICKE) is entered
ISODICKE Insulation thickness in mm, default value 0mm
If ISOTYP is not indicated and insulation thickness is  0, default values of (IS-record) are
used.
SM Line mass pipe (without filling and insulation) in kgm. If SM is not entered, the line mass
is calculated by dimension and material parameters.
SMSIO Line mass insulation in kgm. If SMISO is not entered, the line mass is calculated by the
insulation parameters. SMISO is considered only if ISODICKE  0.
NORM Dimension norm (max. 127 char.). Used as norm of the pipes to which it is assigned.


Page 6 ROHR2 SINETZ Neutral Interface

_Record_ types

_Record types for elements_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.1.7 IS Insulation characteristics

Basic parameters of the insulation like density and thickness of the tin-plates are indicated. Insulation
types are assigned by names to the nominal widths (DN-record).
Insulation types with different parameters must have different names.

_Structure_

```
IS NAME GAM DICKEBL GAMBL
```
NAME Name of this insulation type (required) max. 127 characters
GAM Density of insulation in kgm³, default value see _ntr.env_
DICKEBL Thickness of the insulation tin-plate in mm, default value see _ntr.env_
GAMBL Density of the insulation tin-plate in kgm³, default value see _ntr.env_

## 5.2 Record types for elements

The points of the elements are assigned by the node names (see KN-record) or directly by input of the
coordinates.
Coordinates are surrounded by apostrophes, the directions of the coordinates (x , y, z) are separated by
commas. Format ‘x-coord., y-coord., z-coord.’.
If no nominal width is indicated for an element, nominal width is set = DN200.
If no material for the elements is indicated, the material defined in the _ntr.env_ is used.
If no load is indicated, the default values of LAST are used.

Tees
Tees are defined as follows

- Welded on nozzles
    main pipe one RO element or two RO elements, connecting at the branching point. If there is
    only one RO-element (main pipe) the branching point is automatically inserted into the main pipe
    element.
    branch one RO element.
- Reinforced tee
    main pipe one RO element or two RO elements, connecting at the branching point. If there is
    only one RO-element (main pipe) the branching point is automatically inserted into the main pipe
    element.
    branch one TEV element.
- Weldolet
    main pipe one RO element or two RO elements, connecting at the branching point. If there is
    only one RO-element (main pipe) the branching point is automatically inserted into the main pipe
    element.
    branch one TEW element.
- Fitting one TEE-element


ROHR2 SINETZ Neutral Interface Page 7

_Record_ types

_Record types for elements_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.2.1 Buried pipes

The following parameters are used for the definition of the soil coefficient of buried pipes

SOIL_H input of the Soil cover height up to the upper edge of the pipe at buried pipes.
If this column is available and the value  0m it is assumed that a buried pipe is
used. Additional soil coefficients may be added, see below. Missing values are
replaced by standard values. Soil data in accordance with EN 13941 is generated.
SOIL_HW optional input of the distance to the ground water level at buried pipes.
SOIL_GS optional input of the soil weight above ground water level at buried pipes in kNm³
SOIL_GSW optional input of the soil weight below ground water level at buried pipes in kNm³
SOIL_PHI optional input of the friction angle of the soil at buried pipes in deg
SOIL_CUSH_TYPE optional input of the type of cussion acc. to EN 13941 at buried pipes. Possible
inputs are 1 (for type 1, rigid) or 2 (for type 2, mean).
SOIL_CUSH_THK optional input of the cussion thickness

These parameters can be added to all element data sets except of structural sectionsprofiles (PROF)
record

### 5.2.2 RO Straight pipe

_Structure_

```
RO P1 P2 DN MAT LAST TEXT REF LTG BTK
```
P1 Start point of the pipe (name or coordinates) (required)
P2 End point of the pipe (name or coordinates) (required)
DN Nominal width of the pipe (cf. DN-record)
MAT Pipe material
LAST Loads of the pipe (cf. LAST-record)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

### 5.2.3 PROF Profile, structural section

_Structure_

```
PROF P1 P2 MAT TYP ACHSE RI LAST TEXT REF LTG BTK
```
P1 Start point of the profile (name or coordinates) (required)
P2 End point of the pipe (name or coordinates) (required)
MAT Pipe material
TYP Type of profile. All profiles of the ROHR2 database PROFDS and profile input is permitted
(see ROHR2-manual chap. 11.2.10.3)
ACHSE Direction of the profile axis, to be defined (Y or Z, see ROHR2-manual, Table 1.5)
RI Directional vector of the profile axis, defined by ACHSE in the format ‘x,y,z’ (like
coordinates)
LAST Pipe loads (s. LAST-record)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier


Page 8 ROHR2 SINETZ Neutral Interface

_Record_ types

_Record types for elements_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.2.4 BOG pipe bend

_Structure_

```
BOG P1 P2 PT DN MAT LAST TEXT REF LTG BTK NORM
```
P1 Start point of the bend (name or coordinates), required
P2 End point of the bend (name or coordinates), required
PT Point of tangential intersection of the bend (name or coordinates), required
DN nominal width of the bend (cf. DN-record).
MAT bend material
LAST Bend loads (cf. LAST-record)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Norm of the bend (max. 127 char.)
SERIES Input of wall thickness progression at DIN and EN components. Basing on this value the
wall thickness is calculated. Overwrites the wall thickness from DN-record.
SCHED Input of the schedule at ASME components. Basing on this value the wall thickness is
calculated. Overwrites the wall thickness from DN-record.

### 5.2.5 TEV Branch of a reinforced tee

_Structure_

```
TEV P1 P2 DN MAT LAST V TEXT REF LTG BTK
```
P1 Start point of the branch (on the main pipe), (name or coordinates, required
P2 End point of the branch (name or coordinates), required
DN Nominal width of the branch (cf. DN-record)
MAT Material of the branch
LAST Loads of the branch (cf. LAST-record)
V Thickness of the reinforcement at the main pipe in mm (default value 0 mm)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

### 5.2.6 TEW Weldolet

_Structure_

```
TEW P1 P2 DN MAT LAST TYP TEXT REF LTG BTK
```
P1 Start point of the branch (on the main pipe) name or coordinates, required
P2 End point of the branch (name or coordinates), required
DN Nominal width of the branch (cf. DN-record)
MAT Material of the branch
LAST Loads of the branch (cf. LAST-record)
TYP welded on = A, welded = E, (default value A)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier


ROHR2 SINETZ Neutral Interface Page 9

_Record_ types

_Record types for elements_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.2.7 TEE Tee........................................................................................................................................................

_Structure_

```
TEE PH1 PH2 PA1 PA2 DNH DNA MAT LAST TYP TEXT REF LTG BTK NORM
```
PH1 Start point of the main pipe (name or coordinates) (required)
PH2 End point of the main pipe (name or coordinates) (required)
PA1 Start point of the branch (name or coordinates) (required)
PA2 End point of the branch (name or Coordinates) (required)
DNH Nominal width of the main pipe (cf. DN-record)
DNA Nominal width of the branch (cf. DN-record)
MAT Material of the Tee
LAST Loads of the Tee (cf. LAST-record)
TYP = H (default ‘ ‘)
TYP Type of the tee
A extruded weld. tee
B fitting
without presetting welded nozzle (default)

TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Component norm (max. 127 char.)
SERIES Input of wall thickness progression at DIN and EN components. Basing on this value the
wall thickness is calculated. Overwrites the wall thickness from DN-record.
SCHED Input of the schedule at ASME components. Basing on this value the wall thickness is
calculated. Overwrites the wall thickness from DN-record.

### 5.2.8 RED Reducer

_Structure_

```
RED P1 P2 DN1 DN2 MAT LAST TEXT REF LTG BTK NORM
```
P1 Start point of the reducer (name or coordinates) (required)
P2 End point of the reducer (name or coordinates) (required)
DN1 Nominal width at the beginning (P1) of the reducer (cf. DN-record)
DN2 Nominal width at the end (P2) of the reducer (cf. DN-record)
MAT Material of the reducer
LAST Loads of the reducer (cf. LAST-record)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Component norm (max. 127 char.)
SERIES Input of wall thickness progression at DIN and EN components. Basing on this value the
wall thickness is calculated. Overwrites the wall thickness from DN-record.
SCHED Input of the schedule at ASME components. Basing on this value the wall thickness is
calculated. Overwrites the wall thickness from DN-record.


Page 10 ROHR2 SINETZ Neutral Interface

_Record_ types

_Record types for elements_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.2.9 FLA Flange

_Structure_

```
FLA P1 P2 DN MAT LAST GEW TEXT REF LTG BTK NORM
```
P1 Start point of the flange (name or coordinates), required
P2 End point of the flange (gasket) (name or coordinates), required
DN Nominal width of the flange (cf. DN-record)
MAT Material
LAST Flange loads (cf. LAST-record)
GEW Flange weight in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Component norm (max. 127 char.) To be used at EN and ASME flanges without the
pressure rating included in the ROHR2 norm definition or class, e.g. „EN 1092-111“ or
„ASME B16.5“
PN Definition of the pressure rating without prefix „PN“ , e.g. 16 for PN16 at EN flanges
CLASS Definition of the class at ASME flanges

### 5.2.10 FLABL Blind flange

_Structure_

```
FLABL PNAME DN GEW TEXT REF LTG BTK NORM
```
PNAME Node, where the blind flange is located (name or coordinates, required)
DN Nominal width of the elements (s. DN-record)
GEW weight of the element in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Component norm (max. 127 char.) To be used at EN and ASME flanges without the
pressure rating included in the ROHR2 norm definition or class, e.g. „EN 1092-111“ or
„ASME B16.5“
PN Definition of the pressure rating without prefix „PN“ , e.g. 16 for PN16 at EN flanges
CLASS Definition of the class at ASME flanges


ROHR2 SINETZ Neutral Interface Page 11

_Record_ types

_Record types for elements_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.2.11 ARM Instrument

_Structure_

```
ARM P1 P2 PM DN1 DN2 MAT LAST GEW TEXT REF LTG BTK
```
P1 Start point of the instrument (name or coordinates), required
P2 End point of the instrument (name or coordinates), required
PM Center point of the instrument (resp. break point for angle valve)
DN1 Nominal width of the instrument at the beginning (P1) (cf. DN-record)
DN2 Nominal width of the instrument at the end (P2) (cf. DN-record)
MAT Material
LAST Loads of the instrument (cf. LAST-record)
GEW Weight of the instrument in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

The rigidity of the instrument is considered by 3-times wall thickness of DN1 and DN

### 5.2.12 ARMECK Angle valve

_Structure_

##### ARMECK P1 P2 PM DN1 DN2 MAT LAST GEW TEXT REF LTG BTK

P1 Start point of the instrument (name or coordinates), required
P2 End point of the instrument (name or coordinates), required
PM Center point of the instrument (or break point for angle valve)
DN1 Nominal width of the instrument at the beginning (P1) (cf. DN-record)
DN2 Nominal width of the instrument at the end (P2) (cf. DN-record)
MAT Material
LAST Loads of the instrument (cf. LAST-record)
GEW Weight of the instrument in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

The rigidity of the instrument is considered by 3-times wall thickness of DN1 and DN2.


Page 12 ROHR2 SINETZ Neutral Interface

_Record_ types

_Record types for elements_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.2.13 ARM3W 3-way-valve

_Structure_

```
ARM3W P1 P2 P3 PM DN1 DN2 DN3 MAT LAST GEW ;
```
##### ... TEXT REF LTG BTK

P1 .. .P3 End points of the instrument (name or coordinates), required
PM Center point of the instrument (name or coordinates), required
DN1 ... DN3 Nominal widths of the instrument at the points P1 - P3 (cf. DN-record)
MAT Material
LAST Loads of the instrument (cf. LAST-record)
GEW Weight of the instrument in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

The rigidity of the instrument is considered by 3-times wall thickness of DN1 - DN3.

### 5.2.14 ARM4W 4-way-valve

_Structure_

##### ARM4W P1 P2 P3 P4 PM DN1 DN2 DN3 DN4 MAT LAST GEW ;

##### ... TEXT REF LTG BTK

P1 .. .P4 End points of the instrument (name or coordinates), required
PM Center point of the instrument (name or coordinates), required
DN1 ... DN4 Nominal widths of the instrument at the points P1 - P3 (cf. DN-record)
MAT Material
LAST Loads of the instrument (cf. LAST-record)
GEW Weight of the instrument in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

The rigidity of the instrument is considered by 3-times wall thickness of DN1 - DN4.


ROHR2 SINETZ Neutral Interface Page 13

_Record_ types

_Record types for elements_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.2.15 KAX Axial expansion joint

_Structure_

```
KAX P1 P2 DN MAT LAST GEW CD CL CA CT A L D ;
```
##### ... DMAX TEXT REF LTG BTK

P1 Start point of the element (name or coordinates), required
P2 End point of the element (name or coordinates), required
DN Nominal width of the element (cf. DN-record)
MAT Pipe material
LAST Loads of the element (cf. LAST-record)
GEW Weight of the element in kg
CD Spring rate axial [Nmm]
CL Spring rate lateral [Nmm] or -1.0, if no lateral movement is possible
CA Spring rate angular [Nmdeg] or -1.0, if no angular movement is possible
CT Torsion type suspension [Nmdeg] or -1.0, if no torsion type suspension is possible
A Effective section [m²]
L Corrugated length of the bellow [mm]
D Outer diameter of the bellow [mm]
DMAX Max. axial movement [mm]
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier


Page 14 ROHR2 SINETZ Neutral Interface

_Record_ types

_Record types for elements_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.2.16 KANG Angular expansion joint

_Structure_

```
KANG P1 P2 DN MAT LAST GEW CR CA CP CT ;
```
##### ... AMAX ANZRI VERSP TEXT REF LTG BTK

P1 Start point of the element (name or coordinates), required
P2 End point of the element (name or coordinates), required
DN Nominal width of the element (cf. DN-record)
MAT Pipe material
LAST Loads of the element (cf. LAST-record)
GEW Weight of the element in kg
CR Spring rate Cr [Nmbar]
CA Spring rate Ca [Nmdeg]
CP Spring rate Cp [Nm(deg  bar)]
CT Torsion type suspension [Nmdeg] or -1.0, if no torsion type suspension is possible
AMAX max. angular movement [deg]
ANZRI Number of moving directions 1= unidirectional
2= multiple directional (gimbal)
VERSP Vector of bracing direction (only required for unidirectional expansion joints) Format
‘x,y,z’ (like coordinates!). Vector of bracing direction defines the bracing plane.
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

### 5.2.17 BOX Any element with 2 connections

_Structure_

```
BOX P1 P2 DN1 DN2 MAT LAST GEW TEXT REF LTG BTK
```
P1 Start point of the element (name or coordinates) (required)
P2 End point of the element (name or coordinates) (required)
DN1 Nominal width of the element at the beginning (P1) (cf. DN-record)
DN2 Nominal width of the element a the end (P2) (cf. DN-record)
MAT Pipe material
LAST Loads of the element (cf. LAST-record)
GEW Weight of the element in kg
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

The rigidity of the element is considered by 3-times wall thickness of DN1 and DN2.


ROHR2 SINETZ Neutral Interface Page 15

_Record_ types

_Record types for elements_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.2.18 KLAT Lateral expansion joint

_Structure_

```
KLAT P1 P2 DN MAT LAST GEW CR CL CP CT ;
```
##### ... L LMAX ANZRI VERSP TEXT REF LTG BTK

P1 Start point of the element (Name or coordinates), required
P2 End point of the element (Name or coordinates), required
DN Nominal width of the element (cf. DN-record)
MAT Pipe material
LAST Loads of the element (cf. LAST-record)
GEW Weight of the element in kg
CR Spring rate Cr [Nbar]
CL Spring rate Cl [Nmm]
CP Spring rate Cp [N(mm  bar)]
CT torsion type suspension [Nmdeg] or -1.0, if no torsion type suspension is possible
L Bellow distance [mm]
LMAX max. lateral movement [mm]
ANZRI Number of moving directions 1= unidirectional
2= multiple directional (gimbal)
VERSP Vector of bracing direction, format ‘x,y,z’ (like coordinates!)
It defines the bracing plane.
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier

### 5.2.19 BODEN Head, Bottom

_Structure_

```
BODEN PNAME TYP DN GEW S H1 DIR TEXT REF LTG BTK NORM
```
PNAME Node, where the blind flange is located (name or coordinates, required)
TYP Type of component. Possible inputs are
0  torispherical head
1  semi-ellipsoidal head
2  head, cap
DN Nominal width of the element (s. DN-record)
GEW weight of the element in kg
S nominal wall thickness
H1 straight flange
DIR direction vector, format ́x,y,z ́ like coordinates)
TEXT User defined description
REF Reference
LTG Pipeline
BTK Component identifier
NORM Component norm (max. 127 char.)


Page 16 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

## 5.3 Input records for supports

The nodes of supports are assigned by the node names (see KN-record) or directly by input of the
coordinates.
Coordinates are surrounded by apostrophes, the directions of the coordinates (x , y, z) are separated by
commas. Format ‘x-coord., y-coord., z-coord.’.
Supports can be arranged on a pipe element, a connection between two elements at this point is not
required.

### 5.3.1 Hangers

#### 5.3.1.1 SH Rigid hanger

_Structure_

```
SH PNAME L ANZ GEW TEXT REF BTK BASE
```
PNAME Node, where the hanger is placed (name or coordinates), required
L Hanger length in m, default value 0
GEW additional mass of the hanger in kg, default value 0
ANZ Number of hangers (1 or 2), default value 1.
TEXT User defined description
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal hanger. If there is no input the component is
considered as an external hanger.

The vertical translations are fixed.

#### 5.3.1.2 FH Spring hanger

_Structure_

```
FH PNAME CW L MUE RF ANZ GEW TEXT REF BTK BASE
```
PNAME Node, where the hanger is placed (name or coordinates), required
CW Spring constant in Nmm. If CW is not indicated, CW = 1 is set and the spring is selected.
for automatic design
If ANZ  1, the output of the spring rate takes place per spring!
L Hanger length in m, default value 0
MUE Friction coefficient for inherent resistance in hanger direction, default value 0
RF installation load, default value 0. If no installation load is indicated, the ideal installation
load is determined automatically.
ANZ Number of springs (1 or 2), default value 1. For CW the spring rate must always be
entered per spring.
GEW additional mass of the hanger in kg, default value 0
TEXT User defined description
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal hanger. If there is no input the component is
considered as an external hanger.

The vertical translations are fixed.


ROHR2 SINETZ Neutral Interface Page 17

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

#### 5.3.1.3 KH Constant hanger

_Structure_

```
KH PNAME L MUE RF ANZ GEW TEXT REF BTK BASE
```
PNAME Node, where the constant hanger is placed (name or coordinates), required
L Hanger length in m, default value 0
MUE Friction coefficient for inherent resistance in hanger direction, default value 0
RF installation load, default value 0. If no installation load is indicated, the ideal installation
load is determined automatically.
ANZ Number of hangers (1 or 2), default value 1
TEXT User defined description
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal hanger. If there is no input the component is
considered as an external hanger.

The vertical translations are fixed.

### 5.3.2 Rigid supports

Rigid supports may be defined as _general rigid support_ including support directions (record type ST) or as
_support type_ (record types GL, FL, ...).
Part of the support types are fixed support directions, referring to the direction of the pipe segment, where
the support will be inserted.

#### 5.3.2.1 ST General rigid support

Various support conditions can be entered.

_Structure_

```
ST PNAME KOMP1 KOMP2 KOMP3 KOMP4 KOMP5 KOMP6 RIX RIY ;
```
##### ... TEXT REF BTK BASE

PNAME Node, where the support is placed (name or coordinates), required
KOMP1..6 Support components possible are
WX, WY, WZ  translation in X,Y,Z fixed
PX, PY, PZ  rotation around X,Y,Z fixed
At least one component has to be indicated, the sequence is free
RIX X-axis direction in a special coordinate system of the support
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
RIY Y-axis direction in a special coordinate system of the support
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
If a special coordinate systems with RIX and RIY is entered, the support components refer
to the special coordinate system, in other case to the absolute coordinate system.
TEXT User defined description
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.


Page 18 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

#### 5.3.2.2 Definition of support types

All record types for the definition of support types are structured as following

_Structure_

```
[KENN] PNAME TEXT GEW REF BTK BASE MALL MAQ MAV MQA MQV
```
##### ... MVA MVQ SALL SAV SAB SQV SQB SVV SVB

[KENN] Record identifier for the support type, allowable identifiers see below

PNAME Node, where the support is placed (name or coordinates), required
TEXT any description text
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.

MALL  Friction coefficient for all stops of the support.
The input of MAQ, MAV, MQA, MQV, MVA and MVQ overwrites the value
for the direction, entered for MALL.
MAQ  friction coefficient axial stop at transverse movement
MAV  friction coefficient, axial stop at vertical movement
MQA  friction coefficient, lateral stop at axial movement
MQV  friction coefficient, lateral stop at vertical movement
MVA  friction coefficient, vertical stop at axial movement
MVQ  friction coefficient, vertical stop at transverse movement

SALL  Gap for all stops of the support. The input of SAV, SAB, SQV, SQB
SVV and SVB overwrites the value for the direction, entered for SALL.
SAV  gap of support in mm axial, neg
SAB  gap of support in mm axial, pos
SQV  gap of support in mm diagonal horizontal, neg
SQB  gap of support in mm diagonal horizontal, pos
SVV  gap of support in mm vertical, neg
SVB  gap of support in mm vertical, pos

The definitions of support gap and friction are used for support directions only. Parameters of other
directions are not considered. The directions (positive, negative) refer to a local coordinate system,
defined due to ROHR2basic manual, 3.3.2. If the parameters of friction and gap are missing, default
values are set.


ROHR2 SINETZ Neutral Interface Page 19

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

Identifier for the support type

```
Identifier Description Support direction Remarks
FP Fixed point all movements and
torsions
```
```
Friction, gap of support not
considered
GL Slide bearing vertical
FL Guide bearing, ledger vertical, transverse
AX Axial stop axial
QS Transverse stop horizontally transverse
GLAX Slide bearing with axial
stop
```
```
vertical, axial
```
```
FLAX Ledger with axial stop all movements
QSAX Transverse and axial stop horizontally transverse,
axial
```
```
FLVX Guide support in vertical
direction, bearing in global
x-axis
```
```
vertical, transverse to Xa vertical segments only, X must
NOT be vertical axis.
```
```
FLVY Guide support in vertical
direction, bearing in global
y-axis
```
```
vertical, transverse to Ya vertical segments only, Y must
NOT be vertical axis.
```
```
FLVZ Guide support in vertical
direction, bearing in global
z-axis
```
```
vertical, transverse to Za vertical segments only, Z must
NOT be vertical axis.
```
```
FLVXY Guide support in vertical
direction, bearing in global
x- and y-axis
```
```
vertical, transverse to Xa
and Ya
```
```
vertical segments only, Z must
be vertical axis.
```
```
FLVXZ Guide support in vertical
direction, bearing in global
x- and z-axis
```
```
vertical, transverse to Xa
and Za
```
```
vertical segments only, Y must
be vertical axis.
```
```
FLVYZ Guide support in vertical
direction, bearing in global
y- and z-axis
```
```
vertical, transverse to Ya
and Za
```
```
vertical segments only, X must
be vertical axis.
```
```
QSV Transverse stop in vertical
piping
```
```
both transverse
directions
```
```
vertical segments only
```
```
QSVX Transverse stop in vertical
piping in global x-axis
```
```
transverse in Xa vertical segments only, X must
NOT be vertical axis.
QSVY Transverse stop in vertical
piping in global y-axis
```
```
transverse in Ya vertical segments only, Y must
NOT be vertical axis.
QSVZ Transverse stop in vertical
piping in global z-axis
```
```
transverse in Za vertical segments only, Z must
NOT be vertical axis.
```

Page 20 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.3.3 Spring supports

Spring supports may be defined as general spring support including any support directions (record type
FS) or as support type (record types FGL, FFL, ...).
Support types include fixed support directions, referring to the direction of the pipe segment, where the
support will be inserted.

#### 5.3.3.1 FS general spring support

_Structure_

```
FS PNAME CWX CWY CWZ CPX CPY CPZ RIX RIY ;
```
##### ... TEXT REF BTK BASE

PNAME Node, where the support is placed (name or coordinates), required
CWX Spring constant for translation in X-direction in Nmm
CWY Spring constant for translation in Y-direction in Nmm
CWZ Spring constant for translation in Z-direction in Nmm
CPX Spring constant for rotation in X-direction in Nmmrad
CPY Spring constant for rotation in Y-direction in Nmmrad
CPZ Spring constant for rotation in Z-direction in Nmmrad
At least one spring constant has to be indicated.
RIX X-axis direction in a special coordinate system of the support
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
RIY Y-axis direction in a special coordinate system of the support

The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
If a special coordinate systems with RIX and RIY is entered, the support components refer to the special
coordinate system, in other case to the absolute coordinate system.

TEXT User defined description
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.


ROHR2 SINETZ Neutral Interface Page 21

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

#### 5.3.3.2 Definition of support types

All record types for the definition of support types of spring supports are structured as following

##### [KENN] PNAME TEXT CW RF GEW REF BTK BASE MALL MAQ MAV

##### ... MQA MQV MVA MVQ SALL SAV SAB SQV SQB SVV SVB

KENN] Record identifier for the support type, allowable identifiers see below

PNAME Node, where the support is placed (name or coordinates), required
TEXT any description text
CW vertical spring constant in Nmm. If CW is missing, CW = 1 is set and the spring will be
selected for automatic spring design
RF installation load, default value 0. If no installation load is indicated, the ideal installation
load is determined automatically.
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.

MALL  Friction coefficient for all stops of the support.
The input of MAQ, MAV, MQA, MQV, MVA and MVQ overwrite the value for the direction,
entered for MALL.
MAQ  friction coefficient axial stop at transverse movement
MAV  friction coefficient, axial stop at vertical movement
MQA  friction coefficient, lateral stop at axial movement
MQV  friction coefficient, lateral stop at vertical movement
MVA  friction coefficient, vertical stop at axial movement
MVQ  friction coefficient, vertical stop at transverse movement

SALL  Gap for all stops of the support. The input of SAV, SAB, SQV, SQB
SVV and SVB overwrites the value for the direction, entered for SALL.
SAV  gap of support in mm axial, neg
SAB  gap of support in mm axial, pos
SQV  gap of support in mm diagonal horizontal, neg
SQB  gap of support in mm diagonal horizontal, pos
SVV  gap of support in mm vertical, neg
SVB  gap of support in mm vertical, pos

The definitions of support gap and friction are used for support directions only. Parameters of other
directions are not considered. The directions (positive, negative) refer to a local coordinate system,
defined due to ROHR2basic manual, 3.3.2. If the parameters of friction and gap are missing, default
values are set.


Page 22 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

_Identifier for the support type_

```
Identifier Description Support direction Remarks
FGL Spring support vertical
FFL Spring support + bearing vertical, transverse
FGLAX Spring support + axial stop vertical, axial
FFLAX Spring support + bearing +
axial stop
```
```
vertical, transverse,
axial
FFLVX Spring support + bearing in
vertical direction, bearing in
global x-axis
```
```
vertical, transverse to
Xa
```
```
vertical segments only, X
must NOT be vertical axis.
```
```
FFLVY Spring support + bearing in
vertical direction, bearing in
global y-axis
```
```
vertical, transverse to
Ya
```
```
vertical segments only, Y
must NOT be vertical axis.
```
```
FFLVZ Spring support + bearing in
vertical direction, bearing in
global z-axis
```
```
vertical, transverse to
Za
```
```
vertical segments only, Z
must NOT be vertical axis.
```
```
FFLVXY Spring support + bearing in
vertical direction, bearing in
global x and y-axis
```
```
vertical, transverse to
Xa and Ya
```
```
vertical segments only, Z
must be vertical axis.
```
```
FFLVXZ Spring support + bearing in
vertical direction, bearing in
global x- and z-axis
```
```
vertical, transverse to
Xa and Za
```
```
vertical segments only, Y
must be vertical axis.
```
```
FFLVYZ Spring support + bearing in
vertical direction, bearing in
global y- and z-axis
```
```
vertical, transverse to
Ya and Za
```
```
vertical segments only, X
must be vertical axis.
```

ROHR2 SINETZ Neutral Interface Page 23

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.3.4 Constant supports

Constant supports are defined as support types.
Support types include fixed support directions, referring to the direction of the pipe segment, where the
support will be inserted.

#### 5.3.4.1 Definition of support types

All record types for the definition of support types of constant supports are structured as following

##### [KENN] PNAME TEXT RF GEW REF BTK BASE MALL MAQ MAV

##### ... MQA MQV MVA MVQ SALL SAV SAB SQV SQB SVV SVB

KENN] Record identifier for the support type, allowable identifiers see below

PNAME Node, where the support is placed (name or coordinates), required
TEXT any description text
RF installation load, default value 0. If no installation load is indicated, the ideal installation
load is determined automatically.
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.

MALL  Friction coefficient for all stops of the support.
The input of MAQ, MAV, MQA, MQV, MVA and MVQ overwrite the value for the direction,
entered for MALL.
MAQ  friction coefficient axial stop at transverse movement
MAV  friction coefficient, axial stop at vertical movement
MQA  friction coefficient, lateral stop at axial movement
MQV  friction coefficient, lateral stop at vertical movement
MVA  friction coefficient, vertical stop at axial movement
MVQ  friction coefficient, vertical stop at transverse movement

SALL  Gap for all stops of the support. The input of SAV, SAB, SQV, SQB
SVV and SVB overwrites the value for the direction, entered for SALL.
SAV  gap of support in mm axial, neg
SAB  gap of support in mm axial, pos
SQV  gap of support in mm diagonal horizontal, neg
SQB  gap of support in mm diagonal horizontal, pos
SVV  gap of support in mm vertical, neg
SVB  gap of support in mm vertical, pos

The definitions of support gap and friction are used for support directions only. Parameters of other
directions are not considered. The directions (positive, negative) refer to a local coordinate system,
defined due to ROHR2basic manual, 3.3.2. If the parameters of friction and gap are missing, default
values are set.


Page 24 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

Identifier for the support type

```
Identifier Description Support direction Remarks
KGL constant support vertical
KFL constant support +bearing vertical, transverse
KGLAX constant support + axial stop vertical, axial
KFLAX constant support +bearing + axial
stop
```
```
vertical, transverse, axial
```
```
KFLVX constant support + bearing in
vertical direction, bearing in global
x-axis
```
```
vertical, transverse to Xa vertical segments only, X
must NOT be vertical axis.
```
```
KFLVY constant support + bearing in
vertical direction, bearing in global
y-axis
```
```
vertical, transverse to Ya vertical segments only, Y
must NOT be vertical axis.
```
```
KFLVZ constant support + bearing in
vertical direction, bearing in global
z-axis
```
```
vertical, transverse to Za vertical segments only, Z
must NOT be vertical axis.
```
```
KFLVXY constant support + bearing in
vertical direction, bearing in global
x- and y-axis
```
```
vertical, transverse to Xa
and Ya
```
```
vertical segments only, Z
must be vertical axis.
```
```
KFLVXZ constant support + bearing in
vertical direction, bearing in global
x- and z-axis
```
```
vertical, transverse to Xa
and Za
```
```
vertical segments only, Y
must be vertical axis.
```
```
KFLVYZ constant support + bearing in
vertical direction, bearing in global
y- and z-axis
```
```
vertical, transverse to Ya
and Za
```
```
vertical segments only, X
must be vertical axis.
```

ROHR2 SINETZ Neutral Interface Page 25

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

### 5.3.5 Angulating supports

Angulating supports may be defined as general angulating supports including any support directions and
rigidity (record type GS) or as _support type_ (record types GSV, GSQ, ...).
Support types include fixed support directions, referring to the direction of the pipe segment, where the
support will be inserted.

#### 5.3.5.1 GS General Angulating support

_Structure_

GS PNAME RI CW L MUE RF RIX RIY TEXT GEW REF BTK (^) BASE
PNAME Node, where the support is arranged (name or coordinates), required
RI Support direction (required). allowable values are +X, -X, +Y, -Y, +Z, -Z
CW Spring constant in Nmm. Default = 1.e20.
L Length in m, Default = 0
MUE Friction coefficient of the inherent resistance in support direction, Default 0
RF Installation load, Default = 0. The ideal installation load is determined automatically, if not
entered.
RIX X-axis direction in a special coordinate system of the support
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
RIY Y-axis direction in a special coordinate system of the support
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
If a special coordinate systems with RIX and RIY is entered, the support direction refers to
the special coordinate system, in other case to the global coordinate system.
TEXT User defined description
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.

#### 5.3.5.2 Definition of support types of angulating rigid supports

All record types for the definition of support types of angulating rigid supports are structured as following

_Structure_

[KENN] PNAME L TEXT GEW REF BTK (^) BASE
KENN] Record identifier for the support type, allowable identifiers see below
PNAME Node, where the support is placed (name or coordinates), required
L Length in m, Default = 0
TEXT Any description text
GEW  Additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.


Page 26 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

Identifier for the support type

```
Identifier Description Support direction Remarks
GSV Angulating rigid support, vertical vertical
GSQ Angulating rigid support,
transverse
```
```
transverse
```
```
GSAX Angulating rigid support, axial axial
GSQVX Angulating rigid support in vertical
piping transverse in global X-
direction
```
```
transverse to Xa vertical segments only, X
must NOT be vertical axis.
```
```
GSQVY Angulating rigid support in vertical
piping transverse in global Y-
direction
```
```
transverse to Ya vertical segments only, Y
must NOT be vertical axis.
```
```
GSQVZ Angulating rigid support in vertical
piping transverse in global Z-
direction
```
```
transverse to Za vertical segments only, Z
must NOT be vertical axis.
```
The supports are considered as rigid (i.e. spring rate C=1.e20Nmm).

#### 5.3.5.3 Definition of support types of angulating spring supports

All record types for the definition of support types of angulating spring supports are structured as following

_Structure_

[KENN] PNAME CW L MUE RF TEXT GEW REF BTK (^) BASE
KENN] Record identifier for the support type, allowable identifiers see below
PNAME Node, where the support is arranged (name or coordinates), required
CW Spring constant in Nmm. Default = 1.e20.
L Length in m, Default = 0
MUE Friction coefficient of the inherent resistance in support direction, Default 0
RF Installation load, Default = 0. The ideal installation load is determined automatically, if not
entered.
TEXT User defined description
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.
Identifier for the support type
Identifier Description Support direction Remarks
FGSV Angulating spring support, vertical vertical
FGSQ Angulating spring support,
transverse
transverse
FGSAX Angulating spring support, axial axial
FGSQVX Angulating spring support in
vertical piping transverse in global
X-direction
transverse to Xa vertical segments only, X must
NOT be vertical axis.
FGSQVY Angulating spring support in
vertical piping transverse in global
Y-direction
transverse to Ya vertical segments only, Y must
NOT be vertical axis.


ROHR2 SINETZ Neutral Interface Page 27

_Record_ types

_Input records for supports_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

```
FGSQVZ Angulating spring support in
vertical piping transverse in global
Z-direction
```
```
transverse to Za vertical segments only, Z must
NOT be vertical axis.
```
#### 5.3.5.4 Definition of support types of constant angulating supports

All record types for the definition of support types of constant spring supports are structured as following

_Structure_

```
[KENN] PNAME L MUE RF TEXT GEW REF BTK BASE
```
KENN] Record identifier for the support type, allowable identifiers see below
PNAME Node, where the support is arranged (name or coordinates), required
L Length in m, Default = 0
MUE Friction coefficient of the inherent resistance in support direction, Default 0
RF Installation load, Default = 0. The ideal installation load is determined automatically, if not
entered.
TEXT User defined description
GEW  additional mass in kg, default value 0
REF Reference
BTK Component identifier
BASE Enter a name for the base point of an internal support. If there is no input the component
is considered as an external support.

Identifier for the support type

```
Identifier Description Support direction Remarks
KGSV Constant Angulating support,
vertical
```
```
vertical
```
```
KGSQ Constant Angulating support,
transverse
```
```
transverse
```
```
KGSAX Constant Angulating support,
axial
```
```
axial
```
```
KGSQVX Constant Angulating support
vertical piping transverse in global
X-direction
```
```
transverse to Xa vertical segments only, X
must NOT be vertical axis.
```
```
KGSQVY Constant Angulating support
vertical piping transverse in global
Y-direction
```
```
transverse to Ya vertical segments only, Y
must NOT be vertical axis.
```
```
KGSQVZ Constant Angulating support
vertical piping transverse in global
Z-direction
```
```
transverse to Za vertical segments only, Z
must NOT be vertical axis.
```
The supports are considered as weak (i.e. spring rate von C=0Nmm).


Page 28 ROHR2 SINETZ Neutral Interface

_Record_ types

_Input records for supports_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.3.6 NOZZLE Nozzle

Defining a nozzle.

_Structure_

```
NOZZLE PNAME VTYPE VDA VS NDA NS VDIR VDIST1 VDIST2 CWX CWY CWZ
```
```
... CPX CPY CPZ SRIX SRIY LQX LQY LQZ LMX LMY LMZ RIX RIY TEXT
```
```
PNAME Node on the pipe where the nozzle is placed (name or coordinates)
```
```
VTYPE Vessel type
-1= not set
0 = spherical head
1 = cylinder
2 = torispherical head
3 = semi-ellipsoidal head
```
```
VDA Vessel diameter in mm.
```
```
VS Vessel wall thickness in mm
```
```
NDA Nozzle diameter in mm
```
```
NS Nozzle wall thickness in mm.
```
```
VDIR Direction of vessel axis
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
VDIST1 1. Distance to the next stiffening in m
```
```
VDIST2 2. Distance to the next stiffening in m
```
```
CWX..CPZ user defined spring rates in Nmm resp. Nmgrd, considered if no vessel type is
defined.
```
```
SRIX direction of the x axis of a special coordinate system of user defined spring rates’
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
SRIY direction of the y axis of a special coordinate system of user defined spring rates
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
LQX...LMZ optional definition of allowable loads in kN bzw. kNm
```
```
RIX direction of the x axis of a special coordinate system of the nozzle
```
```
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
RIY direction of the y axis of a special coordinate system of the nozzle
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
TEXT User defined description
```

ROHR2 SINETZ Neutral Interface Page 29

_Record_ types

_Other definitions at nodes - input records_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

## 5.4 Other definitions at nodes - input records

The definition of the nodes is carried out analog to _Input records for supports_ , 5.3.

### 5.4.1 ADD_RES additional results

By means of the ADD_RES record additional results at a node are requested. It can be used, e.g. to
highlight special points in the results.

_Structure_

```
ADD_RES PNAME BASE RIX RIY TEXT REF
```
```
PNAME Node where the additional results are to be placed (name or coordinates)
```
```
BASE Node defining the section of the loads in the output (name or coordinates). The
load at point PNAME from direction point BASE is output. BASE must be a point
next to PNAME.
```
```
RIX direction of the x-axis of a special coordinate system for results output.
A directional vector must be entered in the format ‘x-coord., y-coord., z-coord. ’
```
```
RIY direction of the y-axis of a special coordinate system for results output.
A directional vector must be entered in the format ‘x-coord., y-coord., z-coord.’
```
```
TEXT User defined description
```
```
REF Reference
```
### 5.4.2 PMASS Point mass

With PMASS a point mass at a node is defined.

_Structure_

```
PMASS PNAME MASS
```
```
PNAME Node where the mass has to be placed (name or coordinates)
```
```
MASS Mass in kg
```

Page 30 ROHR2 SINETZ Neutral Interface

_Record_ types

_Other definitions at nodes - input records_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

### 5.4.3 IF internal spring

With IF an internal spring at a node is defined.

_Structure_

```
IF PNAME CWX CWY CWZ CPX CPY CPZ RIX RIY TEXT
```
```
PNAME Node where the internal spring has to be placed (name or coordinates)
```
```
CWX..CPZ spring rates in Nmm resp. Nmgrd
```
```
RIX direction of the x axis of a special coordinate system of the internal spring
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
RIY direction of the y axis of a special coordinate system of the internal spring
The vector of direction is indicated, format ‘x-coord., y-coord., z-coord.’
```
```
TEXT User defined description
```

ROHR2 SINETZ Neutral Interface Page 31

_Configuration_ file ntr.env

_Format of the file ntr.env_

SIGMA Ingenieurgesellschaft mbH [httpwww.rohr2.com](httpwww.rohr2.com)

## 6 Configuration file ntr.env

The file ntr.env is used for the configuration of the NTR files translation. The file ntr.env is searched in the
working directory at first, after that in the directory ROHR2R2WIN or SINETZSINETZW.

## 6.1 Format of the file ntr.env

The file is subdivided into sections. A section starts with the name of the section in square brackets (e.g..
[Material]). The name must start in the first column.
Each section may contain any number of parameter definitions following the section name
Parameters are entered like this
Name = Wert
The parameters needs to be set in apostrophes if the parameter name or value contain space characters
(blanks). The name must start in the first column. Comments can be entered after inserting a C in the first
column.

## 6.2 Defined sections

These sections are defined

### 6.2.1 Default - definition of the settings

At this place the settings for the data import are made. These pre-settings are used if there is specific
value in the parameter ́s records
Settings can be made for these parameters

- Material ROHR2 material name
- OuterDiam Outer diameter [mm]
- WallThk Wall thickness [mm]
- DesignPress Design pressure [bar]
- DesignTemp Design temperature [°C]
- InsDens Insulation density [kgm³]
- InsPlateThk Thickness of the insulation plate [mm]
- InsPlateDens Density of the plate [kgm³]

### 6.2.2 Material - conversion table for material names

Material names from the NTR file are translated into material names taken from the ROHR2 material filet.
Format
[Material_NTR] = [Material_ROHR2]

Any number of material assignments can be entered
Example
[Material]
’A 106 B’ = SA106B


Page 32 ROHR2 SINETZ Neutral Interface

_Example_ Neutral Interface

_Defined sections_

[httpwww.rohr2.com](httpwww.rohr2.com) SIGMA Ingenieurgesellschaft mbH

## 7 Example Neutral Interface

C Example neutral interface
C ====================================
C
C General Settings
GEN TMONT=20 EB=-Z UNITKT=MM
C Order no..
AUFT TEXT='00000000'
C Kopftext
TEXT TEXT='Beispieldatei'
C
C Isoliertypen
IS NAME=ISO1 GAM=130 DICKEBL=1 GAMBL=7850
C Nennweiten
DN NAME=DN200 DA=219.1 S=6.3 ISOTYP=ISO1 ISODICKE=80
DN NAME=DN300 DA=323.9 S=6.3 ISOTYP=ISO1 ISODICKE=100
C
C Belastung
LAST NAME=LAST1 PA=25 PB=20 TA=300 TB=280 GAMMED=800
C
C Rohr-Elemente
RO P1=1 P2=2 DN=DN200 MAT=13CrMo44 LAST=LAST1
RO P1=2 P2=3 DN=DN200 MAT=13CrMo44 LAST=LAST1
BOG P1=3 P2=5 PT=4 DN=DN200 MAT=13CrMo44 LAST=LAST1 TEXT='Bogen DN200'
RO P1=5 P2=6 DN=DN200 MAT=13CrMo44 LAST=LAST1
RED P1=6 P2=7 DN1=DN200 DN2=DN300 MAT=13CrMo44 LAST=LAST1 TEXT='Reduzierung’
RO P1=7 P2=8 DN=DN300 MAT=13CrMo44 LAST=LAST1
C
C Randbedingungen
FP PNAME=1
FL PNAME=2
FL PNAME=8
GL PNAME=6
C
C Koordinaten, Punkt-Definitionen
KN NAME=1 X=0 Y=0 Z=0
KN NAME=2 BNAME=1 X=5000
KN NAME=3 BNAME=2 X=5000
KN NAME=4 BNAME=3 X=305
KN NAME=5 BNAME=4 Y=305
KN NAME=6 BNAME=5 Y=5000
KN NAME=7 BNAME=6 Y=100
KN NAME=8 BNAME=7 Y=1000

C