Suggested logical groupings derived from FJV Dynamiske Komponenter.csv

- Generic Tee/Branching: "Lige afgrening", "Afgrening med spring", "Afgrening, parallel", "Svejsetee", "Preskobling tee", "Stikafgrening".
- Studs/Svanehals: "Afgreningsstuds", "Svanehals" (uses RIGHTSIZE from belongs-to alignment).
- Bue rør: BUEROR1/BUEROR2 (mid-station + length handling).
- Reduktion: future dedicated handler (size array dependency).
- Svejsning: future dedicated handler (rotation, scaling to kappe, numbering overlap logic already exists elsewhere).
- Valves and end caps: share similar attribute mapping; can be included in Generic until customization needed.

Add new handlers by implementing IBlockDetailer and registering in BlockDetailingOrchestrator order-sensitive list.


