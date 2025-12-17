using System.Collections.Generic;
using System.Linq;
using SaleDetail.Domain.Exceptions;

namespace SaleDetail.Application.Validators
{
    public static class ErrorMapping
    {
        public static Dictionary<string, string> MapToDictionary(FluentResults.Result result)
        {
            var dict = new Dictionary<string, string>();

            foreach (var error in result.Errors)
            {
                var parts = error.Message.Split('|', 2);
                if (parts.Length == 2)
                {
                    var field = parts[0];
                    var msg = parts[1];
                    if (!dict.ContainsKey(field))
                        dict[field] = msg;
                }
                else
                {
                    if (!dict.ContainsKey("General"))
                        dict["General"] = error.Message;
                }
            }

            return dict;
        }

        public static void ThrowIfFailed(FluentResults.Result result, string mainMessage = "Errores de validación")
        {
            if (result.IsFailed)
            {
                var errorsDict = MapToDictionary(result);
                throw new ValidationException(mainMessage, errorsDict);
            }
        }
    }
}
