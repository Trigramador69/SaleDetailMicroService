using System.Collections.Generic;

namespace SaleDetail.Domain.Exceptions
{
    public class ValidationException : DomainException
    {
        public Dictionary<string, string> Errors { get; }

        public ValidationException(string message, Dictionary<string, string> errors)
            : base(message)
        {
            Errors = errors;
        }
    }
}
