namespace PirateSlop.EditorTools
{
    public static class ShipLibreFractureAdapter
    {
        public const string Revision = "374197229591a3946b9d8bce44df882bdd413dbc";
        public static string PrepareHullPrototype() => ShipWoodFractureSetup.Prepare(100);
    }
}