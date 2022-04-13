using System;
using MapTo;
using TestConsoleApp.Data.Models;

namespace TestConsoleApp.ViewModels
{
    [Map(typeof(User), MappingDirection.From)]
    //[Map(typeof(UserDto), MappingDirection.From)]
    public class UserViewModel
    {
        [MapProperty(TargetPropertyName = nameof(User.Id), TargetTypeName = typeof(User))]
        [MapProperty(TargetPropertyName = nameof(UserDto.Id), TargetTypeName = typeof(UserDto))]
        [MapTypeConverter(typeof(IdConverter), TargetTypeName = typeof(UserDto))]
        [MapTypeConverter(typeof(IdConverter), TargetTypeName = typeof(User))]
        public string Key { get; set; }

        //[IgnoreProperty]
        public DateTimeOffset RegisteredAt { get; set; }

        [MapProperty(TargetPropertyName = nameof(User.Name), TargetTypeName = typeof(User))]
        [MapProperty(TargetPropertyName = nameof(UserDto.Name), TargetTypeName = typeof(UserDto))]
        public string TestName { get; set; }
        
        //[IgnoreProperty(TargetTypeName = typeof(User))]
        public ProfileViewModel Profile { get; set; }

        public class IdConverter : ITypeConverter<int, string>
        {
            public string Convert(int source, object[]? converterParameters) => $"{source:X}";
        }
    }
}