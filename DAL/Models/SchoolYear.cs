using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Models
{
    public class SchoolYear
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public int ExamYear {  get; set; }
        public Status SchoolYearStatus {  get; set; }
        public enum Status
        {

        }
    
    }
}
