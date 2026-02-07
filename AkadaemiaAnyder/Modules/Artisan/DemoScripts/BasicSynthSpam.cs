using AkadaemiaAnyder.Modules.Artisan.CraftingLogic;
using AkadaemiaAnyder.Modules.Artisan.RawInformation.Character;

namespace DemoScripts;

public class BasicSynthSpam : Solver
{
    public override Recommendation Solve(CraftState craft, StepState step) => new(Skills.BasicSynthesis);
}
