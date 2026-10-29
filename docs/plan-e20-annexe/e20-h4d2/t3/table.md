| id | mutant | verdict | red | first killers (class.test x cases) |
|---|---|---|---|---|
| C01 | delay not decremented | KILLED | 34 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x6, FireTome1AiTests.C63 x2, FireTome1AiTests.K2 x2 |
| C02 | delay decremented by 2 | KILLED | 34 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x6, FireTome1AiTests.C63 x2, FireTome1AiTests.K2 x2 |
| C03 | ForceZ -3 px -> -2 px | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x6, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| C04 | ForceZ written before the delay test | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x6, FireTome1AiTests.C63 x2, FireTome1WorldTests.W1 x2 |
| C05 | no-target draw 9 -> 8 | KILLED | 26 | FireTome1WorldTests.W2 x6, FireTome1WorldTests.W6 x6, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| C06 | no-target wait stored without the draw | KILLED | 29 | FireTome1WorldTests.W2 x6, FireTome1WorldTests.W6 x6, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2 |
| C07 | type & 0xF -> & 0x7 | KILLED | 2 | FireTome1AiTests.K1, FireTome1AiTests.K5 |
| C08 | mask ClassA 1 -> 5 | KILLED | 2 | FireTome1AiTests.C63, FireTome1AiTests.K5 |
| C09 | identity not stored | KILLED | 11 | FireTome1WorldTests.W2 x6, FireTome1WorldTests.W3c x2, FireTome1AiTests.C63, FireTome1AiTests.K1 |
| C10 | dead target not forgotten (the status is not tested) | KILLED | 5 | FireTome1AiTests.C63 x2, FireTome1WorldTests.W2 x2, FireTome1AiTests.K1 |
| C11 | target forgotten only when its status is 3 or more... kept when 2 or 3 | KILLED | 2 | FireTome1AiTests.C63, FireTome1AiTests.K1 |
| C12 | +0x274 = 2 -> 3 after a turn | KILLED | 28 | FireTome1AiTests.K2 x17, FireTome1WorldTests.W2 x6, FireTome1WorldTests.W3c x2, FireTome1AiTests.C63 |
| C13 | turn step 4 -> 3 | KILLED | 27 | FireTome1AiTests.K2 x15, FireTome1WorldTests.W2 x6, FireTome1AiTests.C63 x2, FireTome1WorldTests.W3c x2 |
| C14 | turn threshold < 4 -> < 3 | KILLED | 15 | FireTome1WorldTests.W2 x6, FireTome1AiTests.K2 x5, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| C15 | turn threshold <= 4 (declared equivalent: the equal case is the step itself) | SURVIVES | 0 |  |
| C16 | turn up step 4 -> 5 | KILLED | 19 | FireTome1AiTests.K2 x8, FireTome1WorldTests.W2 x6, FireTome1WorldTests.W3c x2, FireTome1AiTests.C63 |
| C17 | first result: last instead of first | KILLED | 8 | FireTome1WorldTests.W2 x2, FireTome1AiTests.C63, FireTome1AiTests.K1, FireTome1AiTests.K4 |
| C18 | +0x274 = 2 also when the direction is unchanged | KILLED | 12 | FireTome1WorldTests.W2 x6, FireTome1AiTests.K2 x2, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| C19 | turn deferred: no turn in the acquisition tick | KILLED | 11 | FireTome1WorldTests.W2 x6, FireTome1WorldTests.W3c x2, FireTome1AiTests.C63, FireTome1AiTests.K1 |
| D01 | counter mask & 7 -> & 15 | KILLED | 33 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x6, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2 |
| D02 | draw range 32 -> 16 | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D03 | ball Z + 16 px -> + 8 px | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D04 | ForceZ of the ball 0x80000 -> 0x40000 | KILLED | 32 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D05 | +0x274 = 0x10 -> 0x0F | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D06 | +0x278 (target slot) not cleared at the birth | KILLED | 1 | FireTome1DriverTests.D2 |
| D07 | death of the column -> animation 0x33 (hold) instead of 0x34 | KILLED | 28 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D08 | identity ignored (only the missing pointer counts) | KILLED | 28 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D09 | phase 1 without the guard | KILLED | 8 | FireTome1WorldTests.W6 x5, FireTome1DriverTests.C3, FireTome1DriverTests.D1, FireTome1ProductionTests.P3 |
| D10 | no chaining of phase 1 at the birth | KILLED | 34 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2 |
| D11 | null column answers 0 instead of 1 | KILLED | 4 | FireTome1DriverTests.D1, FireTome1DriverTests.D2, FireTome1ProductionTests.P2, FireTome1WorldTests.W7 |
| D12 | phase 2 waits for 0x33 instead of 0x34 | KILLED | 28 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| D13 | guard accepts 0x34 (TomeSlopeGuard) | KILLED | 11 | EarthTome1AiTests.D1 x2, EarthTome2AiTests.T2 x2, EarthTome1AiTests.D3, EarthTome2AiTests.T6 |
| D14 | guard without the slope (TomeSlopeGuard) | KILLED | 9 | Hero.TomeStatesTests.T4_TheGuardOfTheSlope_CancelsInDeepWaterAndOutsideTheCast_AnswersZeroInTheCast x2, EarthTome1AiTests.D1, EarthTome1AiTests.D3, EarthTome2AiTests.T2 |
| E01 | without | 0x40 | KILLED | 28 | EarthTome1WorldTests.W5 x8, EarthTome1WorldTests.W3 x6, EarthTome1WorldTests.W2 x4, EarthTome1WorldTests.W6 x4 |
| E02 | ~0x10 instead of ~0x30 | KILLED | 30 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| E03 | destroy without the end of the animation | KILLED | 60 | EarthTome1WorldTests.W5 x8, FireTome1WorldTests.W2 x8, EarthTome1WorldTests.W3 x7, FireTome1WorldTests.W6 x7 |
| E04 | destroy when the end of the animation alone | KILLED | 2 | EarthTome1AiTests.E1 x2 |
| E05 | animation set to 2 instead of 1 | KILLED | 62 | EarthTome1WorldTests.W5 x8, FireTome1WorldTests.W2 x8, EarthTome1WorldTests.W3 x7, FireTome1WorldTests.W6 x7 |
| E06 | (E, 3) encore {16} | KILLED | 33 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1AiTests.E2 x2, FireTome1WorldTests.W1 x2 |
| E07 | E3 widened to 24 | KILLED | 2 | EarthTome1AiTests.E2, FireTome1AiTests.E2 |
| K01 | counter read AFTER the increment (c+1) & 7 | KILLED | 36 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1DriverTests.D2 x3, FireTome1WorldTests.W8 x3 |
| K02 | draw consumed only if the ball is born | KILLED | 3 | FireTome1DriverTests.D1, FireTome1DriverTests.D2, FireTome1WorldTests.W7 |
| K03 | column pointer stored only when not null | KILLED | 1 | FireTome1DriverTests.D2 |
| K04 | phase advanced before the identity is stored (the identity of the previous cast is seen) | KILLED | 34 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W8 x3, FireTome1WorldTests.W1 x2 |
| K05 | guard result ignored in phase 1 (cancel done, the spell goes on) | KILLED | 3 | FireTome1DriverTests.C3, FireTome1DriverTests.D1, FireTome1WorldTests.W6 |
| K06 | guard result of phase 0 ignored | KILLED | 3 | FireTome1DriverTests.C3, FireTome1DriverTests.D1, FireTome1DriverTests.D2 |
| K07 | column of phase 0 born at hero Z + 16 px | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| K08 | column born with the direction of the hero | KILLED | 29 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| P01 | (C, 63) also registered for 17 | KILLED | 1 | FireTome1AiTests.E2 |
| P02 | turn by the shortest way, wrapped (the refused correction) | KILLED | 20 | FireTome1AiTests.K2 x10, FireTome1WorldTests.W2 x6, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| P03 | wrap in the d < cur branch: ((d - cur) & 31) < 4 | KILLED | 15 | FireTome1WorldTests.W2 x6, FireTome1AiTests.K2 x5, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| P04 | wrap in the d > cur branch: ((cur - d) & 31) < 4 | KILLED | 14 | FireTome1WorldTests.W2 x6, FireTome1AiTests.K2 x4, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| P05 | count fixed to 63 in the binary-mode entry (the count given is ignored) | KILLED | 2 | FireTome1AiTests.K4, FireTome1WorldTests.W3 |
| P06 | the query is called despite a target | KILLED | 13 | FireTome1WorldTests.W2 x6, FireTome1AiTests.C63 x2, FireTome1WorldTests.W3c x2, FireTome1AiTests.K1 |
| P07 | target remembered by its slot alone (looked up in the table) | KILLED | 2 | FireTome1AiTests.C63, FireTome1AiTests.K1 |
| P08 | ItemSpawnedEntity tested by Slot < 0 alone | KILLED | 1 | FireTome1DriverTests.D2 |
| P09 | ItemSpawnedEntity tested by the Destroyed status alone | KILLED | 1 | FireTome1DriverTests.D2 |
| P10 | death of the column read one tick too early (FlagToDestroy counts) | KILLED | 1 | FireTome1DriverTests.D2 |
| P11 | birth of the ball before the guard | KILLED | 37 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1DriverTests.D2 x3, FireTome1WorldTests.W8 x3 |
| P12 | C63 run at the tick of the birth | KILLED | 31 | FireTome1WorldTests.W2 x8, FireTome1WorldTests.W6 x7, FireTome1WorldTests.W1 x2, FireTome1WorldTests.W3 x2 |
| X01 | types 20 / 23 swapped in the cancellation | KILLED | 14 | FireTome1AiTests.C2 x5, FireTome1WorldTests.W6 x5, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X02 | effect 19 -> 18 | KILLED | 10 | FireTome1WorldTests.W6 x4, FireTome1AiTests.C2 x2, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X03 | effect variant 2 -> 1 | KILLED | 10 | FireTome1WorldTests.W6 x4, FireTome1AiTests.C2 x2, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X04 | balls -> FlagToDestroy (status 4) instead of Deactivated (3) | KILLED | 12 | FireTome1WorldTests.W6 x5, FireTome1AiTests.C2 x3, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X05 | +0x20 (blocker) not cleared | KILLED | 5 | FireTome1AiTests.C2 x2, FireTome1AiTests.C1, FireTome1DriverTests.C3, FireTome1DriverTests.D1 |
| X06 | loop exclusive of the last slot | KILLED | 14 | FireTome1AiTests.C2 x5, FireTome1WorldTests.W6 x5, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X07 | status 3 balls also cancelled | KILLED | 1 | FireTome1AiTests.C2 |
| X08 | balls cancelled by position instead of type 23 (any type but 20) | KILLED | 4 | FireTome1AiTests.C2 x2, FireTome1AiTests.C1, FireTome1DriverTests.D1 |
| X09 | effect Z without the binary frame (+ 1) | KILLED | 10 | FireTome1WorldTests.W6 x4, FireTome1AiTests.C2 x2, FireTome1AiTests.C1, FireTome1DriverTests.C3 |
| X10 | column destroyed BEFORE the effect (declared equivalent: the effect takes coordinates) | SURVIVES | 0 |  |
| X11 | column destroyed with the break effect (two-argument destruction -1) | KILLED | 7 | FireTome1AiTests.C2 x4, FireTome1AiTests.C1, FireTome1DriverTests.C3, FireTome1DriverTests.D1 |
