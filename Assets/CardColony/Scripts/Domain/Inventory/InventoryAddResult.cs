namespace CardColony.Inventory
{
    public readonly struct InventoryAddResult
    {
        public int AcceptedQuantity { get; }
        public int RejectedQuantity { get; }
        public bool IsComplete => RejectedQuantity == 0;

        public InventoryAddResult(int acceptedQuantity, int rejectedQuantity)
        {
            AcceptedQuantity = acceptedQuantity;
            RejectedQuantity = rejectedQuantity;
        }
    }
}
