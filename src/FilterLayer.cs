using PeterHan.PLib.Options;

namespace KeysNotIncluded
{
    /// <summary>
    /// The filter layers shown in the tool filter menu (top right while Deconstruct, Cancel,
    /// Prioritize etc. are selected). The game identifies each layer by a string key; which keys a
    /// tool offers depends on the tool. <see cref="FilterMenu"/> maps these values to those keys.
    /// </summary>
    public enum FilterLayer
    {
        [Option("Game default", "Leave the filter as the game sets it.")]
        GameDefault,

        [Option("All", "Every layer.")]
        All,

        [Option("Buildings", "Buildings only (no wires, pipes, rails or automation).")]
        Buildings,

        [Option("Wires", "Power wires.")]
        Wires,

        [Option("Liquid pipes", "Liquid pipes.")]
        LiquidPipes,

        [Option("Gas pipes", "Gas pipes.")]
        GasPipes,

        [Option("Conveyor rails", "Conveyor rails.")]
        ConveyorRails,

        [Option("Automation", "Automation wires.")]
        Automation,

        [Option("Background buildings", "Drywall, tempshift plates and other background buildings.")]
        Backwall,

        [Option("Tiles", "Tiles (only in game versions that have this filter).")]
        Tiles,

        [Option("Dig orders", "Dig orders (Cancel tool).")]
        DigOrders,

        [Option("Mop and sweep orders", "Mop and sweep orders (Cancel tool).")]
        Cleaning,

        [Option("Construction", "Building construction orders (Cancel / Prioritize).")]
        Construction,
    }
}
