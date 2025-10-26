namespace EPS.Discount.Core.Models;

public enum UseCodeResult
{
    Success = 0,
    NotFound = 1,
    AlreadyUsed = 2,
    InvalidFormat = 3,
    UnknownError = 4
}