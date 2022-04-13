using MapTo;

namespace MapTo.Integration.Tests.Data.Models
{
    [Map(typeof(MapTo.Integration.Tests.Data.ViewModels.EmployeeViewModelTo), MappingDirection.To)]
    public class EmployeeTo
    {
        private ManagerTo _manager;

        public int Id { get; set; }

        public string EmployeeCode { get; set; }

        public ManagerTo Manager
        {
            get => _manager;
            set
            {
                if (value == null)
                {
                    _manager.Employees.Remove(this);
                }
                else
                {
                    value.Employees.Add(this);
                }

                _manager = value;
            }
        }
    }
}