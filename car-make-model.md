# Car Make & Model Reference (New Zealand, 1990+)

Reference data for seeding the CarMake/CarModel tables. Scope: every vehicle
make that has been available in the New Zealand market (new-vehicle
distribution and/or significant used-vehicle/JDM grey import volume) with
model nameplates that were on sale from 1990 onward. Model names are the
specific nameplate used in-market (e.g. "Corolla Fielder", not just
"Corolla-derived wagon"); trim levels (GX, GXL, Limited, etc.) and body-style
suffixes are intentionally excluded — those belong on a variant/trim field,
not the model field.

Methodology note: compiled from manufacturer NZ sites, NZ dealer/importer
listings (GWM NZ, KGM NZ, Omoda Jaecoo NZ, Winger Motors), automotive trade
press (Driven, AutoTrader NZ, CarExpert NZ), and detailed model-history
knowledge, then cross-checked make-by-make against Wikipedia's "List of ___
vehicles" reference pages for every high-volume NZ make (Toyota, Lexus,
Honda, Nissan, Mazda, Mitsubishi, Suzuki, Subaru, Isuzu, Daihatsu, Hyundai,
Kia, Ford, Holden, Volkswagen, Audi, BMW, Mercedes-Benz, Mini, Porsche,
Volvo, Land Rover, Jaguar, Jeep, Peugeot, Renault, Citroen, Skoda) to catch
nameplates a knowledge-only pass would miss — e.g. performance sub-brand
models that are registered/marketed as distinct nameplates rather than trim
levels (Audi S3/RS6/SQ7, BMW M2/M4/M8, Mercedes-AMG C63/G63/AMG GT, Ford
Focus ST/RS), and standalone nameplates like the Nissan GT-R (dropped the
"Skyline" prefix from the R35 generation in 2007), Toyota's GR Yaris/GR
Corolla/GR86, and Jaguar's XJ220. Given the sheer size of the NZ used-import
(JDM) fleet and the remaining lower-volume/exotic makes that were not
independently cross-checked against Wikipedia, this aims to be as exhaustive
as practically achievable rather than certified complete down to the last
limited-run JDM variant — treat as a strong seed list to be extended as gaps
are found in production data.

```json
{
  "Toyota": ["Starlet", "Corolla", "Corolla Fielder", "Corolla Runx", "Corolla Spacio", "Corolla Cross", "Corolla Axio", "Sprinter", "Corona", "Corona Exsior", "Carina", "Carina ED", "Caldina", "Camry", "Camry Solara", "Vista", "Avensis", "Cressida", "Crown", "Crown Majesta", "Mark II", "Mark X", "Chaser", "Cresta", "Verossa", "Progres", "Celica", "Supra", "MR2", "Soarer", "Aristo", "Altezza", "Century", "Hilux", "Land Cruiser 70", "Land Cruiser 80", "Land Cruiser 100", "Land Cruiser 200", "Land Cruiser 300", "Land Cruiser Prado", "4Runner", "Hilux Surf", "RAV4", "Highlander", "Kluger", "Yaris", "Yaris Cross", "Vitz", "Echo", "Prius", "Prius C", "Aqua", "Prius V", "Prius+", "C-HR", "bZ4X", "Avalon", "Vios", "Passo", "86", "GT86", "Verso", "Fortuner", "Isis", "Sienta", "Raize", "Harrier", "Venza", "Granvia", "Hiace", "Estima", "Previa", "Tarago", "Alphard", "Vellfire", "Noah", "Voxy", "Ipsum", "Wish", "Rukus", "Belta", "Will Vi", "Will Cypha", "Blade", "bB", "Porte", "Spade", "Roomy", "Tank", "Rush", "FJ Cruiser", "MasterAce", "LiteAce", "TownAce", "Dyna", "Coaster", "Land Cruiser Cygnus", "GR Yaris", "GR Corolla", "GR86", "Mirai"],
  "Lexus": ["IS", "ES", "GS", "LS", "RC", "RC F", "LC", "CT", "UX", "NX", "RX", "GX", "LX", "RZ", "LFA", "HS", "LBX", "TX", "LM", "IS F", "GS F"],
  "Honda": ["Civic", "Civic Type R", "Accord", "Accord Euro", "Ballade", "City", "Jazz", "Fit", "Insight", "CR-V", "HR-V", "Vezel", "ZR-V", "Integra", "Integra Type R", "Prelude", "NSX", "S2000", "S660", "Odyssey", "Stream", "FR-V", "Edix", "Stepwgn", "Elysion", "CR-Z", "Legend", "Domani", "Torneo", "Orthia", "Airwave", "Fit Shuttle", "Freed", "Mobilio", "Vamos", "Life", "N-Box", "N-One", "e:NP1", "Concerto", "CR-X del Sol", "Element", "Passport", "Pilot"],
  "Nissan": ["Sunny", "Sentra", "Pulsar", "Almera", "Bluebird", "Bluebird Sylphy", "Sylphy", "Primera", "Maxima", "Cefiro", "Skyline", "Skyline GT-R", "GT-R", "Laurel", "Gloria", "Cedric", "Fuga", "Fairlady Z", "350Z", "370Z", "Z", "Silvia", "180SX", "200SX", "Presea", "Avenir", "Wingroad", "AD", "Note", "Tiida", "Latio", "March", "Micra", "Leaf", "Ariya", "Juke", "Qashqai", "Dualis", "X-Trail", "Murano", "Pathfinder", "Terrano", "Navara", "Patrol", "Safari", "Elgrand", "Serena", "Presage", "Bassara", "Liberty", "Prairie", "Cube", "Figaro", "Pao", "S-Cargo", "Stagea", "Cima", "President", "Vanette", "Caravan", "Homy", "Largo", "Kicks", "Xterra", "Quest", "Magnite", "Terra"],
  "Mazda": ["121", "323", "323 Astina", "626", "929", "RX-7", "RX-8", "MX-5", "MX-6", "MX-3", "Xedos 6", "Xedos 9", "Millenia", "Eunos 500", "Eunos 800", "Familia", "Capella", "Premacy", "Demio", "Verisa", "Atenza", "Axela", "Mazda2", "Mazda3", "Mazda6", "CX-3", "CX-30", "CX-5", "CX-7", "CX-8", "CX-9", "CX-50", "CX-60", "CX-70", "CX-80", "CX-90", "Tribute", "BT-50", "Bongo", "Biante", "MPV", "Titan", "Roadster", "Carol", "AZ-1", "Cosmo", "Lantis", "Sentia", "Luce"],
  "Mitsubishi": ["Mirage", "Lancer", "Lancer Evolution", "Colt", "Galant", "Diamante", "Sigma", "Magna", "Verada", "Eclipse", "3000GT", "GTO", "Pajero", "Pajero iO", "Pajero Mini", "Montero", "Outlander", "ASX", "RVR", "Delica", "Triton", "L200", "Challenger", "Pajero Sport", "Legnum", "Chariot", "Space Wagon", "Space Star", "Grandis", "Dion", "i-MiEV", "eK Wagon", "Minica", "Colt Plus", "Airtrek", "FTO", "Debonair", "Town Box", "Starion"],
  "Suzuki": ["Swift", "Swift Sport", "Alto", "Cultus", "Baleno", "Liana", "Aerio", "Ignis", "Splash", "Celerio", "S-Cross", "SX4", "Vitara", "Grand Vitara", "Jimny", "Samurai", "Sierra", "Escudo", "Wagon R", "Every", "Carry", "Cappuccino", "Cara", "X-90", "Kizashi", "Across", "Swace", "Cervo", "MR Wagon", "e Vitara", "Fronx", "Victoris", "Xbee", "Brezza"],
  "Subaru": ["Leone", "Impreza", "Impreza WRX", "WRX", "WRX STI", "WRX S4", "Legacy", "Outback", "Forester", "XV", "Crosstrek", "Levorg", "BRZ", "SVX", "Vivio", "Justy", "Domingo", "R1", "R2", "Sambar", "Pleo", "Stella", "Exiga", "Tribeca", "B4", "Traviq", "Ascent", "Solterra", "Rex", "Dex", "Trezia", "Lucra"],
  "Isuzu": ["Bighorn", "Trooper", "D-Max", "MU-X", "Wizard", "Rodeo", "Aska", "Gemini", "Fargo", "Amigo", "VehiCROSS", "Faster"],
  "Daihatsu": ["Charade", "Applause", "Sirion", "Storia", "Terios", "Rocky", "Feroza", "Rugger", "Mira", "Move", "Tanto", "Copen", "Hijet", "YRV", "Pyzar", "Naked", "Materia", "Boon", "Coo", "Atrai", "Cuore", "Sonica", "Wake", "Taft", "Cast", "Gran Max", "Luxio", "Xenia", "Ayla", "Thor", "Be-go"],
  "Ford": ["Laser", "Telstar", "Falcon", "Fairmont", "Fairlane", "LTD", "Festiva", "Escort", "Focus", "Focus ST", "Focus RS", "Fiesta", "Mondeo", "Sierra", "Cougar", "Probe", "Puma", "Ka", "Territory", "Ranger", "Courier", "Everest", "Kuga", "EcoSport", "Explorer", "Edge", "Maverick", "Transit", "Econovan", "Capri", "Corsair", "Streetka", "Galaxy", "Escape", "Mustang", "Mustang Mach-E", "Bronco"],
  "Holden": ["Commodore", "Calais", "Berlina", "Statesman", "Caprice", "Astra", "Barina", "Vectra", "Combo", "Cruze", "Captiva", "Colorado", "Trax", "Equinox", "Trailblazer", "Rodeo", "Jackaroo", "Frontera", "Apollo", "Nova", "Viva", "Epica", "Adventra", "Monaro", "Ute", "Calibra", "Camira", "Cascada", "Acadia", "Monterey", "Tigra"],
  "Chevrolet": ["Camaro", "Corvette", "Cruze", "Captiva", "Colorado", "Silverado", "Tahoe", "Suburban", "Trailblazer", "Malibu", "Spark", "Aveo", "Epica", "Volt", "Bolt"],
  "Chrysler": ["Neon", "Sebring", "300C", "300M", "Concorde", "Voyager", "Grand Voyager", "PT Cruiser", "Crossfire", "Valiant", "Vision"],
  "Jeep": ["Cherokee", "Grand Cherokee", "Wrangler", "Compass", "Patriot", "Renegade", "Gladiator", "Avenger", "Commander", "Comanche", "Liberty", "Grand Wagoneer"],
  "Dodge": ["Journey", "Caliber", "Nitro", "Avenger", "Charger", "Challenger", "Viper", "Durango"],
  "Cadillac": ["Escalade", "CTS", "ATS", "XT5", "Lyriq"],
  "GMC": ["Yukon", "Sierra"],
  "Tesla": ["Model S", "Model 3", "Model X", "Model Y", "Cybertruck"],
  "Volkswagen": ["Golf", "Golf GTI", "Golf R", "Polo", "Polo GTI", "Passat", "Passat CC", "Arteon", "Jetta", "Bora", "Vento", "Scirocco", "Beetle", "New Beetle", "Touareg", "Tiguan", "Touran", "Sharan", "Caddy", "Transporter", "Amarok", "Up!", "Fox", "Lupo", "Corrado", "Karmann Ghia", "Multivan", "Caravelle", "T-Cross", "T-Roc", "ID.3", "ID.4", "ID.5", "Phaeton", "Eos"],
  "Audi": ["80", "90", "100", "A1", "A3", "A4", "A5", "A6", "A7", "A8", "Q2", "Q3", "Q5", "Q7", "Q8", "TT", "TT RS", "R8", "e-tron", "e-tron GT", "Q4 e-tron", "Q6 e-tron", "Cabriolet", "Coupe", "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8", "RS2 Avant", "RS3", "RS4", "RS5", "RS6", "RS7", "RS Q3", "RS Q8", "SQ2", "SQ5", "SQ7", "SQ8"],
  "BMW": ["1 Series", "2 Series", "2 Series Active Tourer", "2 Series Gran Coupe", "3 Series", "4 Series", "5 Series", "6 Series", "7 Series", "8 Series", "X1", "X2", "X3", "X4", "X5", "X6", "X7", "XM", "Z1", "Z3", "Z4", "Z8", "M2", "M3", "M4", "M5", "M6", "M8", "X3 M", "X5 M", "X6 M", "i3", "i4", "i5", "i7", "iX", "iX1", "iX2", "iX3", "i8"],
  "Mercedes-Benz": ["A-Class", "B-Class", "C-Class", "E-Class", "S-Class", "CLA", "CLS", "CLK", "CL", "CLE", "GLA", "GLB", "GLC", "GLE", "GLS", "G-Class", "SLK", "SLC", "SL", "ML", "R-Class", "Viano", "Vito", "Sprinter", "X-Class", "EQA", "EQB", "EQC", "EQE", "EQS", "A45", "C63", "E63", "S63", "G63", "SLS AMG", "AMG GT", "AMG GT S", "AMG GT R", "AMG GT 4-Door Coupe"],
  "Mini": ["Cooper", "Cooper S", "One", "Clubman", "Countryman", "Paceman", "Coupe", "Roadster", "Convertible", "John Cooper Works"],
  "Porsche": ["911", "928", "944", "968", "Boxster", "Cayman", "Cayenne", "Macan", "Panamera", "Taycan", "918 Spyder", "Carrera GT", "959"],
  "Volvo": ["240", "440", "460", "480", "740", "760", "850", "940", "960", "S40", "S60", "S70", "S80", "S90", "V40", "V50", "V60", "V70", "V90", "XC40", "XC60", "XC70", "XC90", "C30", "C70", "EX30", "EX90"],
  "Saab": ["900", "9-3", "9000", "9-5"],
  "Peugeot": ["106", "107", "108", "205", "206", "207", "208", "301", "305", "306", "307", "308", "405", "406", "407", "408", "504", "505", "508", "605", "607", "806", "807", "1007", "2008", "3008", "4007", "4008", "5008", "RCZ", "iOn", "Partner", "Expert", "Boxer"],
  "Citroen": ["AX", "ZX", "Xantia", "BX", "Saxo", "Xsara", "Xsara Picasso", "C1", "C2", "C3", "C3 Aircross", "C4", "C4 Aircross", "C4 Cactus", "C4 Picasso", "C5", "C5 Aircross", "C5 X", "C6", "C8", "C-Crosser", "Berlingo", "DS3", "DS4", "DS5"],
  "Renault": ["19", "21", "Clio", "Megane", "Laguna", "Scenic", "Espace", "Twingo", "Kangoo", "Trafic", "Master", "Koleos", "Captur", "Arkana", "Fluence", "Latitude", "Fuego", "Safrane", "Avantime", "Vel Satis", "Modus", "Sandero", "Wind", "Talisman", "Twizy", "Zoe", "Kadjar", "Alaskan"],
  "Fiat": ["Uno", "Tipo", "Punto", "Bravo", "Brava", "Marea", "Barchetta", "Coupe", "Panda", "500", "500X", "500L", "Doblo", "Ducato", "Multipla", "Croma", "Stilo", "Freemont"],
  "Alfa Romeo": ["33", "75", "145", "146", "155", "156", "159", "164", "166", "GT", "GTV", "Spider", "Giulia", "Giulietta", "Stelvio", "Brera", "MiTo", "4C"],
  "Lancia": ["Delta", "Dedra", "Thema", "Y", "Kappa"],
  "Skoda": ["Favorit", "Felicia", "Fabia", "Octavia", "Superb", "Roomster", "Yeti", "Rapid", "Kodiaq", "Karoq", "Kamiq", "Scala", "Citigo", "Enyaq", "Slavia"],
  "Seat": ["Ibiza", "Cordoba", "Toledo", "Leon", "Alhambra", "Arosa", "Marbella", "Ateca", "Arona", "Tarraco"],
  "Cupra": ["Formentor", "Leon", "Born", "Ateca"],
  "Opel": ["Insignia", "Astra", "Corsa", "Zafira", "Meriva", "Antara"],
  "Land Rover": ["Defender", "Discovery", "Discovery Sport", "Range Rover", "Range Rover Sport", "Range Rover Evoque", "Range Rover Velar", "Freelander"],
  "Jaguar": ["XJ", "XJS", "XJ6", "XJ8", "XJ12", "XJR", "XJ220", "XK", "XKR", "X-Type", "S-Type", "XF", "XE", "F-Type", "F-Pace", "E-Pace", "I-Pace"],
  "Rover": ["200", "213", "214", "216", "218", "220", "400", "414", "416", "420", "600", "620", "800", "820", "825", "827", "25", "45", "75", "Metro", "Maestro", "Montego"],
  "MG": ["MGF", "MG TF", "ZR", "ZS", "ZT", "MG3", "MG5", "MG6", "HS", "GS", "RX5", "RX8", "Marvel R", "MG4", "Cyberster"],
  "Daewoo": ["Cielo", "Nexia", "Espero", "Lanos", "Nubira", "Leganza", "Matiz", "Tacuma", "Kalos"],
  "Hyundai": ["Excel", "Accent", "Elantra", "Lantra", "Sonata", "Grandeur", "Azera", "Getz", "i20", "i30", "i30 N", "i40", "Veloster", "Veloster N", "Kona", "Kona N", "Tucson", "Santa Fe", "Santa Cruz", "Palisade", "ix35", "ix55", "Terracan", "Galloper", "Trajet", "Matrix", "Atos", "Amica", "iMax", "iLoad", "Staria", "Ioniq", "Ioniq 5", "Ioniq 5 N", "Ioniq 6", "Venue", "Creta", "Casper", "Bayon", "Exter", "Coupe", "Tiburon", "Scoupe", "Pony", "Stellar"],
  "Kia": ["Pride", "Mentor", "Sephia", "Shuma", "Cerato", "Cerato Koup", "Rio", "Picanto", "Optima", "K5", "K8", "K9", "Magentis", "Credos", "Clarus", "Carens", "Ceed", "Proceed", "XCeed", "Rondo", "Carnival", "Grand Carnival", "Sedona", "Sorento", "Sportage", "Soul", "Stonic", "Seltos", "Niro", "EV6", "EV9", "Stinger", "Pregio", "Mohave", "Borrego", "Venga", "Telluride", "Tasman"],
  "SsangYong": ["Musso", "Korando", "Korando Family", "Rexton", "Actyon", "Actyon Sports", "Kyron", "Chairman", "Rodius", "Stavic", "Tivoli", "XLV"],
  "KGM": ["Torres", "Torres EVX", "Rexton", "Musso", "Korando"],
  "Genesis": ["G70", "G80", "G90", "GV60", "GV70", "GV80"],
  "Proton": ["Saga", "Wira", "Satria", "Persona", "Perdana", "Gen-2", "Savvy", "Waja", "Preve", "Iriz", "Exora", "X50", "X70"],
  "Tata": ["Xenon", "Indica"],
  "Mahindra": ["Pik-Up", "Scorpio", "XUV500", "Bolero"],
  "GWM": ["Steed", "Wingle", "V240", "V200", "X200", "X240", "Ora", "Ora 03", "Cannon", "Cannon Alpha", "Cannon Hi4-T", "Tank 300", "Tank 500"],
  "Haval": ["H2", "H6", "H6 GT", "H7", "H9", "Jolion", "Jolion Max"],
  "Chery": ["Tiggo 2", "Tiggo 4", "Tiggo 7", "Tiggo 8", "Tiggo 8 Pro", "Arrizo", "QQ"],
  "Jaecoo": ["J7"],
  "Omoda": ["C5", "C9", "E5"],
  "LDV": ["T60", "G10", "V80", "D90", "Deliver 9", "eT60", "MIFA 9"],
  "BYD": ["Atto 3", "Dolphin", "Seal", "Shark 6", "Sealion 6", "Sealion 7"],
  "Foton": ["Tunland"],
  "Dongfeng": ["Nammi 01", "Box"],
  "Infiniti": ["Q30", "Q50", "QX70", "FX"],
  "Polestar": ["Polestar 2", "Polestar 3", "Polestar 4"],
  "Smart": ["Fortwo", "Forfour", "Roadster"],
  "Abarth": ["500", "595", "695"],
  "Ferrari": ["348", "355", "360", "430", "458", "488", "F8", "SF90", "Portofino", "Roma", "812", "California", "GTC4Lusso"],
  "Lamborghini": ["Diablo", "Murcielago", "Gallardo", "Huracan", "Aventador", "Urus"],
  "Maserati": ["Ghibli", "Quattroporte", "GranTurismo", "Levante", "MC20"],
  "Bentley": ["Continental GT", "Flying Spur", "Bentayga", "Mulsanne", "Arnage"],
  "Rolls-Royce": ["Silver Spirit", "Silver Seraph", "Phantom", "Ghost", "Wraith", "Dawn", "Cullinan"],
  "Aston Martin": ["DB7", "DB9", "DB11", "DBS", "Vantage", "Rapide", "DBX"]
}
```
