using System;
using System.Collections.Generic;
using System.Text;
using MapTo;

namespace TestConsoleApp.Data.Models
{
    [Map(typeof(ViewModels2.ManagerViewModel), MappingDirection.To)]
    public class Manager: Employee
    {
        //[IgnoreProperty]
        public int Level { get; set; }

        public IEnumerable<Employee> Employees { get; set; } = Array.Empty<Employee>();
    }
}
