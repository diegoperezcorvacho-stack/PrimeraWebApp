using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Data; // Asegura compatibilidad de tipos de datos en la conexión
using PrimeraWebApp.Models;
using System.Text.Json;

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
            var listaCategorias = new List<dynamic>();
            var listaProductos = new List<dynamic>();

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();

                    // 1. Obtener lista de categorías (ID y Nombre)
                    string queryCategorias = "SELECT id, nombre FROM categoria ORDER BY nombre ASC";
                    using (MySqlCommand cmdCat = new MySqlCommand(queryCategorias, conexion))
                    {
                        using (MySqlDataReader lectorCat = cmdCat.ExecuteReader())
                        {
                            while (lectorCat.Read())
                            {
                                listaCategorias.Add(new
                                {
                                    Id = Convert.ToInt32(lectorCat["id"]),
                                    Nombre = lectorCat["nombre"].ToString()
                                });
                            }
                        }
                    }

                    // 2. Obtener productos con su stock y categoría asignada
                    string queryProductos = "SELECT id, nombre, stock, precio, categoria_id FROM producto";
                    using (MySqlCommand cmdProd = new MySqlCommand(queryProductos, conexion))
                    {
                        using (MySqlDataReader lectorProd = cmdProd.ExecuteReader())
                        {
                            while (lectorProd.Read())
                            {
                                listaProductos.Add(new
                                {
                                    Id = Convert.ToInt32(lectorProd["id"]),
                                    Nombre = lectorProd["nombre"].ToString(),
                                    Stock = Convert.ToInt32(lectorProd["stock"]),
                                    Precio = Convert.ToDecimal(lectorProd["precio"]),
                                    CategoriaId = lectorProd["categoria_id"] != DBNull.Value ? (int?)Convert.ToInt32(lectorProd["categoria_id"]) : null
                                });
                            }
                        }
                    }
                }

                // Pasar ambas listas a la vista mediante ViewBag
                ViewBag.ListaCategorias = listaCategorias;
                ViewBag.ListaProductos = listaProductos;
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al leer categorías y productos: " + ex.Message;
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

        [HttpPost]
        public IActionResult VenderProducto(List<int> productoIds, List<int> cantidades, string nombreCliente)
        {
            if (productoIds == null || productoIds.Count == 0)
            {
                TempData["MensajeError"] = "Por favor, seleccione al menos un producto para vender.";
                return RedirectToAction("Ventas");
            }

            string clienteFinal = string.IsNullOrWhiteSpace(nombreCliente) ? "Cliente General" : nombreCliente.Trim();
            string codigoBoleta = "BOL-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            decimal totalCobrado = 0;
            var listaDetalles = new List<object>();

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();

                    using (var transaccion = conexion.BeginTransaction())
                    {
                        try
                        {
                            for (int i = 0; i < productoIds.Count; i++)
                            {
                                int prodId = productoIds[i];
                                int cantidad = cantidades[i];

                                if (cantidad <= 0) continue;

                                string queryBuscar = "SELECT nombre, precio, stock FROM producto WHERE id = @id";
                                string nombreProd = "";
                                decimal precioUnitario = 0;
                                int stockActual = 0;

                                using (MySqlCommand cmdBuscar = new MySqlCommand(queryBuscar, conexion, transaccion))
                                {
                                    cmdBuscar.Parameters.AddWithValue("@id", prodId);
                                    using (var reader = cmdBuscar.ExecuteReader())
                                    {
                                        if (reader.Read())
                                        {
                                            nombreProd = reader["nombre"].ToString();
                                            precioUnitario = Convert.ToDecimal(reader["precio"]);
                                            stockActual = Convert.ToInt32(reader["stock"]);
                                        }
                                        else
                                        {
                                            throw new Exception($"El producto con ID {prodId} no existe.");
                                        }
                                    }
                                }

                                if (stockActual < cantidad)
                                {
                                    throw new Exception($"Stock insuficiente para {nombreProd}.");
                                }

                                // 1. Descontar stock
                                string queryUpdate = "UPDATE producto SET stock = stock - @cantidad WHERE id = @id";
                                using (MySqlCommand cmdUpdate = new MySqlCommand(queryUpdate, conexion, transaccion))
                                {
                                    cmdUpdate.Parameters.AddWithValue("@cantidad", cantidad);
                                    cmdUpdate.Parameters.AddWithValue("@id", prodId);
                                    cmdUpdate.ExecuteNonQuery();
                                }

                                decimal subtotal = precioUnitario * cantidad;
                                totalCobrado += subtotal;

                                // 2. INSERT EN VENTA (Incluyendo la columna comprador)
                                string queryInsertVenta = @"INSERT INTO venta (producto_id, cantidad_vendida, precio_venta, ganancia_total, comprador, fecha_venta) 
                                                   VALUES (@producto_id, @cantidad, @precio, @ganancia, @comprador, NOW())";

                                using (MySqlCommand cmdVenta = new MySqlCommand(queryInsertVenta, conexion, transaccion))
                                {
                                    cmdVenta.Parameters.AddWithValue("@producto_id", prodId);
                                    cmdVenta.Parameters.AddWithValue("@cantidad", cantidad);
                                    cmdVenta.Parameters.AddWithValue("@precio", precioUnitario);
                                    cmdVenta.Parameters.AddWithValue("@ganancia", subtotal);
                                    cmdVenta.Parameters.AddWithValue("@comprador", clienteFinal); // <-- AQUÍ SE GUARDA EL NOMBRE
                                    cmdVenta.ExecuteNonQuery();
                                }

                                // 3. Preparar lista para el JSON de la boleta
                                listaDetalles.Add(new
                                {
                                    Producto = nombreProd,
                                    Cantidad = cantidad,
                                    PrecioUnitario = precioUnitario,
                                    Subtotal = subtotal
                                });
                            }

                            // 4. Guardar en la tabla boleta
                            string jsonDetalle = JsonSerializer.Serialize(listaDetalles);
                            string queryBoleta = @"INSERT INTO boleta (codigo_boleta, usuario_nombre, total, detalle_json) 
                                           VALUES (@codigo, @usuario, @total, @detalle)";

                            using (MySqlCommand cmdBoleta = new MySqlCommand(queryBoleta, conexion, transaccion))
                            {
                                cmdBoleta.Parameters.AddWithValue("@codigo", codigoBoleta);
                                cmdBoleta.Parameters.AddWithValue("@usuario", clienteFinal);
                                cmdBoleta.Parameters.AddWithValue("@total", totalCobrado);
                                cmdBoleta.Parameters.AddWithValue("@detalle", jsonDetalle);
                                cmdBoleta.ExecuteNonQuery();
                            }

                            transaccion.Commit();

                            TempData["MensajeExito"] = $"¡Venta realizada con éxito! Boleta N°: {codigoBoleta}";
                            TempData["GananciaObtenida"] = totalCobrado.ToString("N0");
                        }
                        catch (Exception ex)
                        {
                            transaccion.Rollback();
                            TempData["MensajeError"] = "Error procesando la venta: " + ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error de conexión: " + ex.Message;
            }

            return RedirectToAction("Ventas");
        }

        // 2. NUEVA ACCIÓN: PÁGINA Y BUSCADOR DE HISTORIAL DE BOLETAS
        [HttpGet]
        public IActionResult HistorialBoletas(string buscarCodigo)
        {
            var listaBoletas = new List<dynamic>();

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();
                    string query = "SELECT id, codigo_boleta, usuario_nombre, fecha, total, detalle_json FROM boleta ";

                    if (!string.IsNullOrEmpty(buscarCodigo))
                    {
                        query += "WHERE codigo_boleta LIKE @codigo ";
                    }
                    query += "ORDER BY fecha DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conexion))
                    {
                        if (!string.IsNullOrEmpty(buscarCodigo))
                        {
                            cmd.Parameters.AddWithValue("@codigo", "%" + buscarCodigo.Trim() + "%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                listaBoletas.Add(new
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    Codigo = reader["codigo_boleta"].ToString(),
                                    Usuario = reader["usuario_nombre"].ToString(),
                                    Fecha = Convert.ToDateTime(reader["fecha"]),
                                    Total = Convert.ToDecimal(reader["total"]),
                                    Detalle = reader["detalle_json"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error al cargar historial: " + ex.Message;
            }

            ViewBag.BuscarCodigo = buscarCodigo;
            ViewBag.ListaBoletas = listaBoletas;
            return View();
        }
        [HttpPost]
        public IActionResult EliminarCategoria(string nombreCategoria)
        {
            if (string.IsNullOrEmpty(nombreCategoria))
            {
                TempData["MensajeError"] = "Categoría no válida.";
                return RedirectToAction("Categorias");
            }

            try
            {
                using (MySqlConnection conexion = new MySqlConnection(_connectionString))
                {
                    conexion.Open();

                    using (var transaccion = conexion.BeginTransaction())
                    {
                        try
                        {
                            // 1. Obtener el ID de la categoría por su nombre
                            int categoriaId = 0;
                            string queryBuscarId = "SELECT id FROM categoria WHERE nombre = @nombre";
                            using (MySqlCommand cmdId = new MySqlCommand(queryBuscarId, conexion, transaccion))
                            {
                                cmdId.Parameters.AddWithValue("@nombre", nombreCategoria);
                                var result = cmdId.ExecuteScalar();
                                if (result != null)
                                {
                                    categoriaId = Convert.ToInt32(result);
                                }
                            }

                            if (categoriaId > 0)
                            {
                                // 2. Eliminar primero las ventas vinculadas a los productos de esta categoría
                                string queryVentas = @"DELETE FROM venta 
                                              WHERE producto_id IN (SELECT id FROM producto WHERE categoria_id = @catId)";
                                using (MySqlCommand cmdVentas = new MySqlCommand(queryVentas, conexion, transaccion))
                                {
                                    cmdVentas.Parameters.AddWithValue("@catId", categoriaId);
                                    cmdVentas.ExecuteNonQuery();
                                }

                                // 3. Eliminar los productos pertenecientes a esta categoría
                                string queryProductos = "DELETE FROM producto WHERE categoria_id = @catId";
                                using (MySqlCommand cmdProd = new MySqlCommand(queryProductos, conexion, transaccion))
                                {
                                    cmdProd.Parameters.AddWithValue("@catId", categoriaId);
                                    cmdProd.ExecuteNonQuery();
                                }

                                // 4. Eliminar la categoría
                                string queryCategoria = "DELETE FROM categoria WHERE id = @catId";
                                using (MySqlCommand cmdCat = new MySqlCommand(queryCategoria, conexion, transaccion))
                                {
                                    cmdCat.Parameters.AddWithValue("@catId", categoriaId);
                                    cmdCat.ExecuteNonQuery();
                                }

                                transaccion.Commit();
                                TempData["MensajeExito"] = $"La categoría '{nombreCategoria}', sus productos y su historial de ventas fueron eliminados con éxito.";
                            }
                            else
                            {
                                TempData["MensajeError"] = "No se encontró la categoría especificada.";
                            }
                        }
                        catch (Exception ex)
                        {
                            transaccion.Rollback();
                            TempData["MensajeError"] = "Error al eliminar: " + ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["MensajeError"] = "Error de conexión: " + ex.Message;
            }

            return RedirectToAction("Categorias");
        }
    }
}