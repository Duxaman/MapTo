using MapTo;

namespace TestConsoleApp.Data.Models
{
    [Map(typeof(User), MappingDirection.To)]
    [Map(typeof(ViewModels.UserViewModel), MappingDirection.To)]
    public class UserDto
    {
        [MapProperty(TargetPropertyName = nameof(ViewModels.UserViewModel.Key), TargetTypeName = typeof(ViewModels.UserViewModel))]
        [MapTypeConverter(typeof(IdConverter), TargetTypeName = typeof(ViewModels.UserViewModel))]
        public int Id { get; set; }

        [MapProperty(TargetPropertyName = nameof(ViewModels.UserViewModel.TestName), TargetTypeName = typeof(ViewModels.UserViewModel))]
        public string Name { get; set; }

        public class IdConverter : ITypeConverter<int, string>
        {
            public string Convert(int source, object[]? converterParameters) => $"{source:X}";
        }
    }
}