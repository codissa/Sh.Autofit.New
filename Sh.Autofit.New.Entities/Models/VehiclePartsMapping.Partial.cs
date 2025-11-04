using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sh.Autofit.New.Entities.Models
{
    public partial class VehiclePartsMapping
    {
        // Navigation to the view entity (read-only)
        public virtual VwPart? PartItemKeyNavigation { get; set; }
    }
}
