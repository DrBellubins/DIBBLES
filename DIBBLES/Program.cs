using DIBBLES.Utils;

namespace DIBBLES;

class Program
{
    static void Main(string[] args)
    {
        #if DEBUG
            // Debug build
            var game = new Engine();
            game.Run();
        #else
            // Release build
            try 
            {
                var game = new Engine();
                game.Run();
            }
            catch (Exception ex)
            {
                Debug.Error(ex.ToString());
            }
        #endif
    }
}