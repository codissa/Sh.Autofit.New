using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sh.Autofit.New.Entities.Models
{
    public partial class ShAutofitContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Map the view as a regular (read-only) entity with a key
            modelBuilder.Entity<VwPart>(e =>
            {
                e.ToView("vw_Parts", "dbo");     // tells EF this is a view (no INSERT/UPDATE/DELETE)
                e.HasKey(x => x.PartNumber);     // make it keyed; EF Power Tools often makes views keyless
                e.Property(x => x.PartNumber).HasColumnName("PartNumber");
            });

            // Wire EF-only relationship: VehiclePartsMapping.PartItemKey -> VwPart.PartNumber
            modelBuilder.Entity<VehiclePartsMapping>(e =>
            {
                e.HasOne(m => m.PartItemKeyNavigation)
                 .WithMany()                                // no back-collection on a view
                 .HasForeignKey(m => m.PartItemKey)        // FK column in VehiclePartsMappings
                 .HasPrincipalKey(p => p.PartNumber);      // key on the view
            });
        }
    }
}
