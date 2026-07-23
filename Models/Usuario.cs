namespace PrimeraWebApp.Models // Reemplaza "TuProyecto" por el nombre real de tu proyecto
{
    public class Usuario
    {
        // El ID es autoincremental en la BD, pero lo necesitamos en C# para identificar cada fila
        public int Id { get; set; }

        // Atributos solicitados en tu Tarea 2
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Contraseña { get; set; } = string.Empty;
    }
}