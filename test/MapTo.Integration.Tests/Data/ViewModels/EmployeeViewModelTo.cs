using MapTo.Integration.Tests.Data.Models;

namespace MapTo.Integration.Tests.Data.ViewModels
{
    public partial class EmployeeViewModelTo
    {
        public int Id { get; set; }

        public string EmployeeCode { get; set; }

        public ManagerViewModelTo Manager { get; set; }
    }
}