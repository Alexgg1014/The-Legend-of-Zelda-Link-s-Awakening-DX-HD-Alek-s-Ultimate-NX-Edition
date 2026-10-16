/*
 * switch_net.c — red HTTPS mínima para el updater in-app (v1.2).
 *
 * Unidad de traducción propia, como el resto de los shims: <switch.h> y
 * <curl/curl.h> arrastran u8/u32 y demás, que no queremos mezclar con el resto.
 *
 * libcurl aquí es la portlib switch-curl de devkitPro, construida contra el
 * backend TLS de libnx (el servicio `ssl` del sistema), así que HTTPS valida
 * contra el almacén de CA de la propia consola: no hay cacert.pem embebido.
 * DNS y proxy salen del driver de sockets. Solo hace falta
 * socketInitializeDefault() una vez.
 *
 * Este fichero es un calco de `switch_net.c` del port de Minish Cap del mismo
 * autor, que lleva en producción desde su v1.3 y está probado en hardware. Lo
 * único que cambia es la superficie: aquí solo exportamos lo que necesita el
 * updater (no hay RetroAchievements), y el progreso vive en un contador estático
 * en vez de pasarse por puntero, para que el lado C# no tenga que fijar memoria.
 *
 * REGLAS DEL PORT que afectan a este fichero:
 *   - Los estáticos mutables van a .bss (sin inicializador), porque el runtime
 *     de NativeAOT deja de solo lectura la primera página de .data al arrancar
 *     y escribir ahí después es un Data Abort (ver HANDOFF §0, commit 1cc467b).
 *   - En C las rutas "sdmc:/..." son correctas (las resuelve el devoptab de
 *     newlib). La regla de no usarlas es para las APIs de ficheros de C#.
 */
#include <switch.h>

#include <curl/curl.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

/* Todos en .bss: sin inicializador y contando desde 0. */
static int s_net_ready;
static unsigned s_progress_permille;

static void nlog(const char *msg) {
    FILE *f = fopen("sdmc:/switch/zelda-ladxhd/update/update.log", "a");
    if (f == NULL) {
        return;
    }
    fputs(msg, f);
    fputc('\n', f);
    fflush(f);
    fclose(f);
}

/* ------------------------------------------------------------------ ciclo de vida */

/* Levanta el driver de sockets. Se llama una sola vez, perezosamente, desde el
 * primer uso del updater: así un arranque normal no paga el ~1-2 MiB de memoria
 * de transferencia que reserva la configuración por defecto. */
int SwitchNetInit(void) {
    Result rc;
    if (s_net_ready) {
        return 1;
    }
    rc = socketInitializeDefault();
    if (R_FAILED(rc)) {
        char msg[96];
        snprintf(msg, sizeof msg, "[net] socketInitializeDefault fallo: 0x%x", (unsigned)rc);
        nlog(msg);
        return 0;
    }
    if (curl_global_init(CURL_GLOBAL_DEFAULT) != CURLE_OK) {
        nlog("[net] curl_global_init fallo");
        socketExit();
        return 0;
    }
    s_net_ready = 1;
    nlog("[net] socket + curl listos");
    return 1;
}

void SwitchNetExit(void) {
    if (!s_net_ready) {
        return;
    }
    curl_global_cleanup();
    socketExit();
    s_net_ready = 0;
}

/* fsdev agrupa las escrituras a la SD: sin este commit, un corte de corriente
 * puede dejar el .nro a medias aunque fclose haya devuelto OK. Se llama tras
 * cada paso durable del updater. */
void SwitchCommitSd(void) {
    fsdevCommitDevice("sdmc");
}

unsigned SwitchHttpProgressPermille(void) {
    return s_progress_permille;
}

/* ------------------------------------------------------------------ GET a memoria */

typedef struct {
    char  *buf;
    size_t cap;   /* capacidad total, incluido el hueco del NUL */
    size_t len;
    int    overflow;
} TextBuf;

static size_t write_text_cb(char *ptr, size_t size, size_t nmemb, void *userdata) {
    size_t incoming = size * nmemb;
    TextBuf *t = (TextBuf *)userdata;
    if (incoming > t->cap - t->len - 1) {
        /* El manifiesto tiene un tamaño acotado por diseño: una respuesta más
         * grande es señal de que algo no es nuestro manifiesto. Se aborta en vez
         * de truncar en silencio y dejar que el parser vea un JSON cortado. */
        t->overflow = 1;
        return 0;
    }
    memcpy(t->buf + t->len, ptr, incoming);
    t->len += incoming;
    t->buf[t->len] = '\0';
    return incoming;
}

/*
 * GET HTTPS bloqueante a un buffer del llamante. Devuelve el código HTTP, o un
 * negativo si falló el transporte. Solo HTTPS, también en las redirecciones.
 */
long SwitchHttpGet(const char *url, char *out, int cap, int *out_len) {
    CURL *curl;
    TextBuf t;
    CURLcode res;
    long status = -3;

    if (out_len) {
        *out_len = 0;
    }
    if (out == NULL || cap <= 1) {
        return -1;
    }
    out[0] = '\0';
    if (!s_net_ready || url == NULL || strncmp(url, "https://", 8) != 0) {
        return -1;
    }
    curl = curl_easy_init();
    if (curl == NULL) {
        return -2;
    }
    memset(&t, 0, sizeof t);
    t.buf = out;
    t.cap = (size_t)cap;

    curl_easy_setopt(curl, CURLOPT_URL, url);
    curl_easy_setopt(curl, CURLOPT_USERAGENT, "LADXHD-Switch-Updater/1");
    curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
    curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 3L);
    curl_easy_setopt(curl, CURLOPT_PROTOCOLS, (long)CURLPROTO_HTTPS);
    curl_easy_setopt(curl, CURLOPT_REDIR_PROTOCOLS, (long)CURLPROTO_HTTPS);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 1L);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 2L);
    curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT, 15L);
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 30L);
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_text_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &t);

    res = curl_easy_perform(curl);
    if (res == CURLE_OK && !t.overflow) {
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &status);
        if (out_len) {
            *out_len = (int)t.len;
        }
    } else if (t.overflow) {
        status = -5;
        nlog("[net] respuesta mas grande de lo permitido");
    } else {
        char msg[160];
        snprintf(msg, sizeof msg, "[net] GET fallo: %s", curl_easy_strerror(res));
        nlog(msg);
    }
    curl_easy_cleanup(curl);
    return status;
}

/* ------------------------------------------------------------------ descarga a fichero */

typedef struct {
    FILE  *file;
    size_t expected;
    size_t written;
    int    failed;
} DownloadCtx;

static size_t write_file_cb(char *ptr, size_t size, size_t nmemb, void *userdata) {
    DownloadCtx *d = (DownloadCtx *)userdata;
    size_t bytes = size * nmemb;
    if (bytes == 0) {
        return 0;
    }
    /* El tamaño anunciado en el manifiesto es un límite duro: ni un byte más.
     * Es la primera de las dos barreras; la segunda es el SHA-256 al acabar. */
    if (d->written > d->expected || bytes > d->expected - d->written ||
        fwrite(ptr, 1, bytes, d->file) != bytes) {
        d->failed = 1;
        return 0;
    }
    d->written += bytes;
    if (d->expected) {
        s_progress_permille = (unsigned)((d->written * 1000u) / d->expected);
    }
    return bytes;
}

/*
 * Descarga en streaming a `dest` (ruta "sdmc:/..."), sin bufferizar el NRO
 * entero en RAM. Solo HTTPS, sin vuelta a HTTP ni en las redirecciones.
 *
 * Sin CURLOPT_TIMEOUT: el NRO pasa de 50 MB y un tope fijo mataría descargas
 * legítimas en conexiones lentas. En su lugar, se corta si el caudal baja de
 * 1 KB/s durante 60 s seguidos, que es lo que de verdad queremos detectar.
 */
long SwitchHttpDownloadFile(const char *url, const char *dest, unsigned long long expected) {
    CURL *curl;
    FILE *out;
    DownloadCtx d;
    CURLcode res;
    long status = -3;
    int fd;

    s_progress_permille = 0;
    if (!s_net_ready || url == NULL || dest == NULL || expected == 0 ||
        strncmp(url, "https://", 8) != 0) {
        return -1;
    }
    out = fopen(dest, "wb");
    if (out == NULL) {
        nlog("[net] no se pudo abrir el fichero de destino");
        return -4;
    }
    curl = curl_easy_init();
    if (curl == NULL) {
        fclose(out);
        return -2;
    }
    memset(&d, 0, sizeof d);
    d.file = out;
    d.expected = (size_t)expected;

    curl_easy_setopt(curl, CURLOPT_URL, url);
    curl_easy_setopt(curl, CURLOPT_USERAGENT, "LADXHD-Switch-Updater/1");
    curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
    curl_easy_setopt(curl, CURLOPT_MAXREDIRS, 3L);
    curl_easy_setopt(curl, CURLOPT_PROTOCOLS, (long)CURLPROTO_HTTPS);
    curl_easy_setopt(curl, CURLOPT_REDIR_PROTOCOLS, (long)CURLPROTO_HTTPS);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 1L);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 2L);
    curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT, 15L);
    curl_easy_setopt(curl, CURLOPT_LOW_SPEED_LIMIT, 1024L);
    curl_easy_setopt(curl, CURLOPT_LOW_SPEED_TIME, 60L);
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_file_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &d);

    res = curl_easy_perform(curl);
    if (res == CURLE_OK && !d.failed && d.written == (size_t)expected) {
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &status);
    } else if (res != CURLE_OK) {
        char msg[160];
        snprintf(msg, sizeof msg, "[net] descarga fallo: %s", curl_easy_strerror(res));
        nlog(msg);
    }
    curl_easy_cleanup(curl);

    if (fflush(out) != 0) {
        status = -5;
    }
    fd = fileno(out);
    if (fd >= 0) {
        (void)fsync(fd);
    }
    if (fclose(out) != 0) {
        status = -5;
    }
    if (status < 200 || status >= 300) {
        /* Ruta de staging fija, nunca el NRO instalado. */
        (void)remove(dest);
        SwitchCommitSd();
        return (status == -3 && d.failed) ? -6 : status;
    }
    SwitchCommitSd();
    s_progress_permille = 1000;
    return status;
}
