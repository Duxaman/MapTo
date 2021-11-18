using System;
using MapTo;
using TestConsoleApp.Common;
using TestConsoleApp.Data.Models;

namespace TestConsoleApp.ViewModels
{
    [MapFrom(typeof(User))]
    [MapFrom(typeof(UserDto))]
    //[MapTo(typeof(UserDto))]
    //[MapTo(typeof(UserInSameProject))]
    [MapTo(typeof(UserInOtherProject))]
    public partial class UserViewModel
    {
        [MapProperty(SourcePropertyName = nameof(User.Id))]
        //[MapProperty(SourcePropertyName = nameof(UserDto.Id), SourceTypeName = "TestConsoleApp.Data.Models.UserDto")]
        //[MapTypeConverter(typeof(IdConverter), SourceTypeName = typeof(UserDto))]
        [MapTypeConverter(typeof(IdConverter))]
        public string Key { get; }
        public DateTimeOffset RegisteredAt { get; set; }

        //[MapProperty(SourcePropertyName = nameof(UserDto.Name))]
        //[MapProperty(SourcePropertyName = nameof(UserDto.Name), SourceTypeName = typeof(UserDto))]
        public string TestName { get; set; }
        
        //[IgnoreProperty(SourceTypeName = typeof(User))]
        public ProfileViewModel Profile { get; set; }

        private class IdConverter : ITypeConverter<int, string>
        {
            public string Convert(int source, object[]? converterParameters) => $"{source:X}";
        }
    }
}