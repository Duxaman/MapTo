using System;
using System.Collections.Generic;
using System.Text;
using MapTo;

namespace TestConsoleApp.Data.Models
{
    //[Map(typeof(ViewModels.EmployeeViewModel), MappingDirection.To)]

    public class Employee
    {
        public int Id { get; set; }

        public string EmployeeCode { get; set; }

        public Manager Manager { get; set; }
    }
}
