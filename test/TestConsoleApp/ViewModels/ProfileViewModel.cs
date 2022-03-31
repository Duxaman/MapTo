using MapTo;
using TestConsoleApp.Data.Models;

namespace TestConsoleApp.ViewModels
{
    [MapFrom(typeof(Profile))]
    public partial class ProfileViewModel
    {
        public string FirstName { get; set; }

        public string LastName { get; set; } //TODO: если поставить init то не будет кэша!
    }
}