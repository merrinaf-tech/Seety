# Seety

A single strip over the Cities: Skylines II HUD that shows the state of your city — and where
**every entry opens what it is about**.

Click a reading and the matching in-game info view opens. Click one that has more behind it and a
window appears: the city's problems grouped by type with the camera jumping to each, the workforce
against the jobs that exist, who lives here by age and education, why zone demand is stuck, which
school is about to overflow.

## What it shows

Thirty-two readings, each switched on or off by clicking the thing itself rather than a checkbox:
happiness, health, unemployment, homelessness, workers, tourists, active problems, electricity,
water, sewage, hospital and crematorium capacity, cemetery space, four school levels, parking,
garbage, landfill, safety, cells, fire safety, four kinds of pollution, post, traffic, land value,
zone demand and transport.

Two rules decide what is in that list:

1. **It must not already be on the vanilla HUD.** Population, money and income are on screen
   permanently, so repeating them buys nothing and costs width.
2. **It must mirror the exact source vanilla uses for the same figure.** A number that disagrees
   with the game's own panels is worse than no number at all.

## Design

**An alert is just a metric with a threshold.** A count is a statistic; the same count with a line
drawn under it is a warning. Treating them as one idea is why this needs one bar and one list
rather than two of each.

**More is always better.** Half of what a city reports is naturally negative — pollution, crime,
cells occupied. Those are inverted and renamed, so a fuller, greener bar always means better and
there is no direction to remember per icon.

**It reads, and it takes you there.** Nothing here writes city state and nothing reaches a
savegame. The only things it changes are which info view is active and where the camera is
looking — both because you asked by clicking.

[`DESIGN.md`](DESIGN.md) has the reasoning behind each decision, including the traps found along
the way.

## Building

```bash
cd UI && npm install     # once
cd UI && npm run build   # whenever the UI changes
dotnet build Seety.sln -c Release
```

The C# build copies `UI/dist` into the output before deploying, so one `dotnet build` produces the
finished package — provided the UI has been built at least once.

Requires the official Cities: Skylines II modding toolchain. `ModPostProcessor` targets .NET 6 with
no roll-forward, so on a machine with only .NET 8 installed set `DOTNET_ROLL_FORWARD=Major` for the
build.

## Licence

MIT — see [`LICENSE`](LICENSE).
