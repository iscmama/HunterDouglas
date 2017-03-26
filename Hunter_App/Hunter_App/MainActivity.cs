using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Util;
using Android.Widget;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hunter_App
{
    [Activity(Label = "Hunter Douglas App", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        string urlWebAPI = "http://138.0.216.3/WebAPI/";//Production
        //string urlWebAPI = "http://138.0.216.3/WebAPITest/";//Test
        ProgressDialog progress;
        static string contenido = string.Empty;
        static readonly string TAG = typeof(MainActivity).FullName;
        Context context = Application.Context;

        protected override void OnCreate(Bundle bundle)
        {
            try
            {
                base.OnCreate(bundle);

                SetContentView(Resource.Layout.Main);

                // Get our button from the layout resource,
                // and attach an event to it
                Button button = FindViewById<Button>(Resource.Id.button1);
                EditText textoUsuario = FindViewById<EditText>(Resource.Id.editText1);
                EditText textoPassword = FindViewById<EditText>(Resource.Id.editText2);

                string usuarioinformacion = Intent.GetStringExtra("usuarioinformacion") ?? string.Empty;

                if (!string.IsNullOrEmpty(usuarioinformacion))
                {
                    UsuarioBO resultado = Newtonsoft.Json.JsonConvert.DeserializeObject<UsuarioBO>(usuarioinformacion);

                    if (resultado != null && !string.IsNullOrEmpty(resultado.Usuario) && !string.IsNullOrEmpty(resultado.Password))
                    {
                        textoUsuario.Text = resultado.Usuario;
                        textoPassword.Text = resultado.Password;
                    }
                }

                button.Click += async delegate
                {
                    button.Enabled = false;

                    if (string.IsNullOrEmpty(textoUsuario.Text))
                    {
                        Toast.MakeText(this, "Ingrese su usuario...", ToastLength.Long).Show();
                        button.Enabled = true;
                        return;
                    }

                    if (string.IsNullOrEmpty(textoPassword.Text))
                    {
                        Toast.MakeText(this, "Ingrese su contraseña...", ToastLength.Long).Show();
                        button.Enabled = true;
                        return;
                    }

                    if (!GetIsInternetAccessAvailable())
                    {
                        Toast.MakeText(context, "Sin Acceso a Internet. Verifique", ToastLength.Long).Show();
                        button.Enabled = true;
                        return;
                    }

                    if (!IsHostReachable())
                    {
                        Toast.MakeText(context, "Sin comunicación con el Servicio. Verifique", ToastLength.Long).Show();
                        button.Enabled = true;
                        return;
                    }

                    progress = new ProgressDialog(this);
                    progress.Indeterminate = true;
                    progress.SetProgressStyle(ProgressDialogStyle.Spinner);
                    progress.SetMessage("Validando información...");
                    progress.SetCancelable(false);
                    progress.Show();

                    string resultado = await LogInuser(textoUsuario.Text, textoPassword.Text);

                    if (string.IsNullOrEmpty(resultado))
                    {
                        var intent = new Intent(this, typeof(ProcesoPrincipalActivity));
                        intent.PutExtra("contenido", contenido);

                        RunOnUiThread(() =>
                        {
                            button.Enabled = true;
                            progress.SetMessage("Cargando contenido...");
                            progress.Hide();
                            progress.Dispose();
                            StartActivity(intent);
                        });
                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            button.Enabled = true;
                            progress.Hide();
                            progress.Dispose();
                            Toast.MakeText(this, resultado, ToastLength.Long).Show();
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                progress.Hide();
                progress.Dispose();
                Toast.MakeText(context, "La aplicación tuvo un incoveniente. Intente más tarde. Error: " + ex.Message, ToastLength.Long).Show();
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (!GetIsInternetAccessAvailable())
            {
                Toast.MakeText(context, "Sin Acceso a Internet. Verifique", ToastLength.Long).Show();
                return;
            }
        }

        private string SignInUser(string usuario, string password)
        {
            string mensaje = string.Empty;
            //cdelacruz@hdmexico.onmicrosoft.com
            //Pa$$w0rd

            try
            {
                var client = new RestClient(urlWebAPI);
                var request = new RestRequest("api/Procesos/AutenticarUsuario", Method.POST);
                request.AddObject(new UsuarioBO { Usuario = usuario, Password = password });
                IRestResponse response = client.Execute(request);
                if (!string.IsNullOrEmpty(response.Content))
                {
                    AutenticarUsuarioResult resultado = Newtonsoft.Json.JsonConvert.DeserializeObject<AutenticarUsuarioResult>(response.Content);
                    if (resultado != null)
                    {
                        if (resultado.autenticado)
                        {
                            contenido = response.Content;
                        }
                        else
                        {
                            mensaje = "Usuario/Contraseña incorrectas...";
                        }
                    }
                }
                else
                {
                    mensaje = "Revise su conexión a internet.";
                }
            }
            catch (Exception ex)
            {
                mensaje = ex.Message;
            }
            return mensaje;
        }

        #region Nueva Implementacion

        private bool GetIsInternetAccessAvailable()
        {
            try
            {
                ConnectivityManager connectivityManager = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
                NetworkInfo networkInfo = connectivityManager.ActiveNetworkInfo;
                return networkInfo.IsConnected;
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo GetIsInternetAccessAvailable: {0}", ex.Message);
                return false;
            }
        }
        public bool IsHostReachable()
        {
            try
            {
                string CheckUrl = "http://138.0.216.3/WebAPI";
                HttpWebRequest iNetRequest = (HttpWebRequest)WebRequest.Create(CheckUrl);
                iNetRequest.Timeout = 3000;
                WebResponse iNetResponse = iNetRequest.GetResponse();
                iNetResponse.Close();

                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, "Error en el metodo IsHostReachable: {0}", ex.Message);
                return false;
            }
        }
        private async Task<string> LogInuser(string usuario, string password)
        {
            string mensaje = string.Empty;

            try
            {
                string urlService = "http://138.0.216.3/WebAPI/api/Procesos/AutenticarUsuario";//Production
                //string urlService = "http://138.0.216.3/WebAPITest/api/Procesos/AutenticarUsuario";//Test

                var uri = new System.Uri(urlService);

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(180);

                    var json = JsonConvert.SerializeObject(new UsuarioBO { Usuario = usuario, Password = password });
                    var contentParams = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(uri, contentParams);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        AutenticarUsuarioResult resultado = JsonConvert.DeserializeObject<AutenticarUsuarioResult>(content);

                        if (resultado != null)
                        {
                            if (resultado.autenticado)
                            {
                                mensaje = resultado.mensaje;
                                contenido = content;
                            }
                            else
                            {
                                mensaje = "Usuario/Contraseña incorrectas...";
                            }
                        }
                        else
                        {
                            mensaje = "Sin respuesta del servicio. Intente más tarde";
                        }
                    }
                    else
                    {
                        mensaje = "El servicio no respondio de forma satisfactoria. Intente más tarde";
                    }
                }
            }
            catch (Exception ex)
            {
                mensaje = "Problemas al consumir el servicio. Error: " + ex.Message;
                Log.Debug(TAG, "Error en el metodo LogInuser: {0}", ex.Message);
            }

            return mensaje;
        }

        #endregion Nueva Implementacion

    }

    public class AutenticarUsuarioResult
    {
        public List<CitaBO> lista { get; set; }
        public bool autenticado { get; set; }
        public string mensaje { get; set; }
    }
    public class UsuarioBO
    {
        public string Usuario { get; set; }
        public string Password { get; set; }
    }
    public class CitaBO : UsuarioBO
    {
        public Guid Id { get; set; }
        public string Tema { get; set; }
        public string StrTipoSeguimiento { get; set; }
        public int TipoSeguimiento { get; set; }
        public string HoraLlegada { get; set; }
        public string HoraSalida { get; set; }
        public double LongitudInicio { get; set; }
        public double LongitudFin { get; set; }
        public double LatitudInicio { get; set; }
        public double LatitudFin { get; set; }

        public int year { get; set; }
        public int month { get; set; }
        public int day { get; set; }
        public int hour { get; set; }
        public int minute { get; set; }

        public override string ToString()
        {
            return Tema;
        }
    }
}