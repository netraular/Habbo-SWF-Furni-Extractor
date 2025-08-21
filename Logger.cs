// Ruta: SimpleExtractor/Logger.cs
namespace SimpleExtractor
{
    public static class Logger
    {
        public static bool IsVerbose { get; set; } = false;

        /// <summary>
        /// Escribe un mensaje en la consola solo si el modo verboso está activado.
        /// </summary>
        public static void Log(string message)
        {
            if (IsVerbose)
            {
                Console.WriteLine(message);
            }
        }

        /// <summary>
        /// Escribe un mensaje en la consola siempre, independientemente del modo verboso.
        /// Útil para mensajes importantes como inicio, fin o errores.
        /// </summary>
        public static void Info(string message)
        {
            Console.WriteLine(message);
        }
    }
}