using FluentResults;

namespace SaleDetail.Application.Validators
{
    public static class ResultExtensions
    {
        public static Result WithFieldError(this Result result, string field, string message)
        {
            return result.WithError($"{field}|{message}");
        }
    }
}
