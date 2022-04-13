using MapTo;

namespace TestConsoleApp.Data.Models
{
    [Map(typeof(TestConsoleApp.ViewModels.ProfileViewModel), MappingDirection.To)]
    public class Profile
    {
        public string FirstName { get; set; }

        //[IgnoreProperty(TargetTypeName = typeof(ViewModels.ProfileViewModel))]
        public string LastName { get; set; }

        [MapProperty(TargetPropertyName = "LastName", TargetTypeName = typeof(TestConsoleApp.ViewModels.ProfileViewModel))]
        public string FullName => $"{FirstName} {LastName}";
    }
}