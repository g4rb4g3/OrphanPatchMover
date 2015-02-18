using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrphanPatchMover
{
  public class OrphanedItemsViewModel
  {
    public List<OrphanedItem> Items { get; private set; }

    public OrphanedItemsViewModel(List<OrphanedItem> items)
    {
      this.Items = items;
    }
  }
}

public class OrphanedItem
{
  public Boolean selected { get; set; }
  public double size { get; set; }
  public String filepath { get; private set; }
  public String filename { get; set; }
  public double percentageToTotal { get; set; }

  public OrphanedItem(String filepath)
  {
    this.filepath = filepath;
  }
}