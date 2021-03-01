using SharpFileSystem.FileSystems;

public class DeDupeLocalFileSystem : PhysicalFileSystem
{
    private string name = "";
    public DeDupeLocalFileSystem(string physicalRoot, string name) : base(physicalRoot)
    {
        this.name = name;
    }

    public override string ToString()
    {
        return name;
    }
}