using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Data; // Asegura compatibilidad de tipos de datos en la conexión
using PrimeraWebApp.Models;

namespace PrimeraWebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // =========================================================================
        // Acción principal: Carga la vista, prueba la conexión y lee las categorías
        // =========================================================================
        public IActionResult Index()
        {
            List<dynamic> categorias = new List<dynamic>();

            using (MySqlConnection conexion = new MySqlConnection(_connectionString))
            {
                try
                {
                    conexion.Open();
                    ViewBag.MensajeConexion = "¡Conexión Web exitosa a MySQL usando appsettings.json!";
                    ViewBag.EstiloConexion = "success";

                    // Cargar categorías para el selector del formulario
                    string query = "SELECT id, nombre FROM categoria ORDER BY nombre ASC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conexion))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                categorias.Add(new
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    Nombre = reader["nombre"].ToString()
                                });
                            }
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    ViewBag.MensajeConexion = $"Error de conexión en el servidor: {ex.Message}";
                    ViewBag.EstiloConexion = "danger";
                }
            }

            ViewBag.ListaCategorias = categorias;
            return View();
        }

        // =========================================================================
        // Acción POST para Registrar Producto e Historial de Stock (Fase 3)
        // =========================================================================
        [HttpPost]
        public IActionResult Insertar(string nombre, int stock, decimal precio, int categoriaId)
        {
            if (stock < 0)
            {
                TempData["MensajeError"] = "Error Operativo: No se pueden registrar existencias negativas.";
                return RedirectToAction("Index");
            }

            // Consulta para insertar el producto incluyendo la categoría seleccionada
            string queryProducto = "INSERT INTO producto (nombre, stock, precio, categoria_id) VALUES (@nom, @stk, @pre, @catId); SELECT LAST_INSERT_ID();";
            string queryHistorial = "INSERT INTO historial (producto_id, cantidad_ingresada, fecha_hora) VALUES (@prodId, @cant, NOW());";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    int nuevoProductoId = 0;

                    using (MySqlCommand cmdProd = new MySqlCommand(queryProducto, conexion))
                    {
                        cmdProd.Parameters.AddWithValue("@nom", nombre);
                        cmdProd.Parameters.AddWithValue("@stk", stock);
                        cmdProd.Parameters.AddWithValue("@pre", precio);
                        cmdProd.Parameters.AddWithValue("@catId", categoriaId);

                        nuevoProductoId = Convert.ToInt32(cmdProd.ExecuteScalar());
                    }

                    using (MySqlCommand cmdHist = new MySqlCommand(queryHistorial, conexion))
                    {
                        cmdHist.Parameters.AddWithValue("@prodId", nuevoProductoId);
                        cmdHist.Parameters.AddWithValue("@cant", stock);
                        cmdHist.ExecuteNonQuery();
                    }
                }

                // Cálculo del valor del lote / ganancias proyectadas o totales
                decimal valorTotalLote = stock * precio;

                TempData["MensajeExito"] = $"¡Insumo '{nombre}' registrado con éxito en la categoría seleccionada!";
                TempData["StockRestante"] = stock; // Notifica cuántos productos quedan
                TempData["ValorVenta"] = valorTotalLote.ToString("N0"); // Notifica cuánto se ha generado
                TempData["ValorLote"] = valorTotalLote.ToString("N0");
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error en el servidor al procesar el insumo: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // CRUD DE CATEGORÍAS (Exigencia Obligatoria Fase 3)
        // =========================================================================

        // 1. Acción GET: Carga la vista para ver y listar las categorías existentes (Read)
        [HttpGet]
        public IActionResult Categorias()
        {
            List<string> listaCategorias = new List<string>();
            string query = "SELECT nombre FROM categoria ORDER BY nombre ASC";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    using (MySqlCommand comando = new MySqlCommand(query, conexion))
                    {
                        conexion.Open();
                        using (MySqlDataReader lector = comando.ExecuteReader())
                        {
                            while (lector.Read())
                            {
                                listaCategorias.Add(lector["nombre"].ToString());
                            }
                        }
                    }
                }
                ViewBag.ListaCategorias = listaCategorias;
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al leer categorías centralizadas: " + ex.Message;
            }

            return View();
        }

        // 2. Acción POST: Procesa el formulario seguro para almacenar una nueva categoría (Create)
        [HttpPost]
        public IActionResult CrearCategoria(string nombreCategoria)
        {
            if (string.IsNullOrWhiteSpace(nombreCategoria))
            {
                TempData["MensajeError"] = "El nombre de la clasificación agrícola no puede quedar vacío.";
                return RedirectToAction("Categorias");
            }

            string query = "INSERT INTO categoria (nombre) VALUES (@nombre)";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    using (MySqlCommand comando = new MySqlCommand(query, conexion))
                    {
                        comando.Parameters.AddWithValue("@nombre", nombreCategoria.Trim());

                        conexion.Open();
                        comando.ExecuteNonQuery(); // Ejecución atómica y obligatoria con ExecuteNonQuery()
                    }
                }
                TempData["MensajeExito"] = $"¡Categoría '{nombreCategoria}' establecida con éxito para el control de la distribuidora!";
            }
            catch (MySqlException ex)
            {
                TempData["MensajeError"] = "Error en base de datos: La categoría ingresada ya existe o está duplicada. " + ex.Message;
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error general del servidor: " + ex.Message;
            }

            return RedirectToAction("Categorias");
        }

        // =========================================================================
        // Gestión de Usuarios (Registro de personal)
        // =========================================================================
        [HttpGet]
        public IActionResult Registrar()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Registrar(Usuario nuevoUsuario)
        {
            string query = "INSERT INTO usuario (nombre, apellido, correo, contraseña) VALUES (@Nombre, @Apellido, @Correo, @Contraseña)";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    using (MySqlCommand comando = new MySqlCommand(query, conexion))
                    {
                        comando.Parameters.AddWithValue("@Nombre", nuevoUsuario.Nombre);
                        comando.Parameters.AddWithValue("@Apellido", nuevoUsuario.Apellido);
                        comando.Parameters.AddWithValue("@Correo", nuevoUsuario.Correo);
                        comando.Parameters.AddWithValue("@Contraseña", nuevoUsuario.Contraseña);

                        conexion.Open();
                        comando.ExecuteNonQuery();
                    }
                }

                TempData["MensajeExito"] = "¡Usuario guardado exitosamente en MySQL mediante ADO.NET!";
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al guardar el usuario: " + ex.Message;
            }

            return RedirectToAction("Registrar");
        }

        // =========================================================================
        // NUEVO: CRUD / MÓDULO DE INICIO DE SESIÓN SEGURO (Fase 3)
        // =========================================================================

        // 1. Acción GET: Muestra la pantalla del formulario web de Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 2. Acción POST: Procesa los datos y valida de forma segura contra la base de datos
        [HttpPost]
        public IActionResult Login(string correo, string contraseña)
        {
            string query = "SELECT nombre, apellido FROM usuario WHERE correo = @correo AND contraseña = @pass";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    using (MySqlCommand comando = new MySqlCommand(query, conexion))
                    {
                        comando.Parameters.AddWithValue("@correo", correo.Trim());
                        comando.Parameters.AddWithValue("@pass", contraseña);

                        conexion.Open();

                        using (MySqlDataReader lector = comando.ExecuteReader())
                        {
                            if (lector.Read())
                            {
                                string nombreCompleto = $"{lector["nombre"]} {lector["apellido"]}";
                                TempData["MensajeExito"] = $"¡Bienvenido al sistema de Comercializadora Arica S.A., {nombreCompleto}!";
                                return RedirectToAction("Index");
                            }
                            else
                            {
                                ViewBag.ErrorLogin = "Correo electrónico o contraseña incorrectos. Intente nuevamente.";
                                return View();
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                ViewBag.ErrorLogin = "Error crítico de conexión con el motor de base de datos: " + ex.Message;
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.ErrorLogin = "Error general del sistema: " + ex.Message;
                return View();
            }
        }

        // =========================================================================
        // NUEVO: Acción de Cierre de Sesión Seguro
        // =========================================================================
        [HttpGet]
        public IActionResult CerrarSesion()
        {
            TempData.Clear();
            TempData["MensajeExito"] = "Sesión cerrada correctamente. Gracias por usar el sistema.";

            return RedirectToAction("Login");
        }

        // =========================================================================
        // VISTA DEDICADA DE VENTAS (GET)
        // =========================================================================
        [HttpGet]
        public IActionResult Ventas()
        {
            List<dynamic> listaProductos = new List<dynamic>();

            string query = @"SELECT p.id, p.nombre, p.stock, p.precio, c.nombre AS categoria 
                             FROM producto p 
                             INNER JOIN categoria c ON p.categoria_id = c.id 
                             WHERE p.stock > 0 
                             ORDER BY c.nombre, p.nombre ASC";

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, conexion))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaProductos.Add(new
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    Nombre = reader["nombre"].ToString(),
                                    Stock = Convert.ToInt32(reader["stock"]),
                                    Precio = Convert.ToDecimal(reader["precio"]),
                                    Categoria = reader["categoria"].ToString()
                                });
                            }
                        }
                    }
                }
                ViewBag.ListaProductos = listaProductos;
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al obtener productos: " + ex.Message;
            }

            return View();
        }

        // =========================================================================
        // MÓDULO DE VENTAS (POST - ÚNICA INSTANCIA REVISADA)
        // =========================================================================
        [HttpPost]
        public IActionResult VenderProducto(int productoId, int cantidadVendida)
        {
            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    int stockActual = 0;
                    decimal precioUnitario = 0;

                    string querySelect = "SELECT stock, precio FROM producto WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(querySelect, conexion))
                    {
                        cmd.Parameters.AddWithValue("@id", productoId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                stockActual = Convert.ToInt32(reader["stock"]);
                                precioUnitario = Convert.ToDecimal(reader["precio"]);
                            }
                            else
                            {
                                TempData["MensajeError"] = "El producto seleccionado no existe.";
                                return RedirectToAction("Ventas");
                            }
                        }
                    }

                    if (stockActual < cantidadVendida)
                    {
                        TempData["MensajeError"] = $"Stock insuficiente. Solo quedan {stockActual} unidades disponibles.";
                        return RedirectToAction("Ventas");
                    }

                    int nuevoStock = stockActual - cantidadVendida;
                    decimal ganancia = cantidadVendida * precioUnitario;

                    string queryUpdate = "UPDATE producto SET stock = @nuevoStock WHERE id = @id";
                    using (MySqlCommand cmdUpdate = new MySqlCommand(queryUpdate, conexion))
                    {
                        cmdUpdate.Parameters.AddWithValue("@nuevoStock", nuevoStock);
                        cmdUpdate.Parameters.AddWithValue("@id", productoId);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    string queryVenta = "INSERT INTO venta (producto_id, cantidad_vendida, precio_venta, ganancia_total, fecha_venta) VALUES (@pId, @cant, @precio, @ganancia, NOW())";
                    using (MySqlCommand cmdVenta = new MySqlCommand(queryVenta, conexion))
                    {
                        cmdVenta.Parameters.AddWithValue("@pId", productoId);
                        cmdVenta.Parameters.AddWithValue("@cant", cantidadVendida);
                        cmdVenta.Parameters.AddWithValue("@precio", precioUnitario);
                        cmdVenta.Parameters.AddWithValue("@ganancia", ganancia);
                        cmdVenta.ExecuteNonQuery();
                    }

                    TempData["MensajeExito"] = $"¡Venta procesada exitosamente! Stock restante: {nuevoStock} unidades.";
                    TempData["GananciaObtenida"] = ganancia.ToString("N0");
                }
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al registrar la venta: " + ex.Message;
            }

            return RedirectToAction("Ventas");
        }
    }
}