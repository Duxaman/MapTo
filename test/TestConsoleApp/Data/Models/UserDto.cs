using MapTo;
using TestConsoleApp.ViewModels;

namespace TestConsoleApp.Data.Models
{
    public class UserDto
    {
        [MapProperty(SourcePropertyName = nameof(UserViewModel.Key))]
        [MapTypeConverter(typeof(IdConverter))]
        public int Id { get; set; }

        public string Name { get; set; }

        private class IdConverter : ITypeConverter<string, int>
        {
            public int Convert(string source, object[]? converterParameters) => 6;
        }
    }
}