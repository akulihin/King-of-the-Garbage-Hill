using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill;

public interface IServiceTransient
{
    Task InitializeAsync();
}