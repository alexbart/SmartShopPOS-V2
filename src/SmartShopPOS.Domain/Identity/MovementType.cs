namespace SmartShopPOS.Domain.Identity;

public enum MovementType
{
    Receipt = 1,
    Sale = 2,
    AdjustmentIncrease = 3,
    AdjustmentDecrease = 4,
    OpeningBalance = 5,
    Return = 6
}