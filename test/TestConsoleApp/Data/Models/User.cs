using System;

namespace TestConsoleApp.Data.Models
{
    public class User
    {
        public int Id { get; set; }

        //[MapProperty(TargetPropertyName = nameof(TestConsoleApp.ViewModels.UserViewModel.TestName))]
        public string Name { get; set; }

        //[IgnoreProperty]
        public DateTimeOffset RegisteredAt { get; set; }

        public Profile Profile { get; set; }
    }
}