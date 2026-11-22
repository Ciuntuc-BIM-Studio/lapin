/**
 * Lapin's own denoiser for the path tracer (docs/plan-render.md, S4), the one the browser and any computer without Intel
 * Open Image Denoise use: an edge-avoiding à-trous wavelet filter (Dammertz et al. 2010) guided by the picture's albedo,
 * normals and depth, on the light demodulated from the albedo (so a brick's texture is never blurred, only the noise of
 * the light falling on it), five passes of a 5 × 5 B3 kernel 1, 2, 4, 8 and 16 pixels apart.
 *
 * Edges stop it: a normal turning (a wall's corner, a brick's relief through its normal map), a jump in depth (one wall
 * in front of another), a change of light (a shadow's edge) that the samples so far already show; the luminance stop
 * tightens as the samples grow (1 / √n), so a well-converged picture is left nearly as it is.
 *
 * The same filter twice: in GLSL for the GPU (ATROUS_FRAGMENT) and in TypeScript (denoiseCpu), the second the tests'
 * reference (a known noisy picture comes out closer to the clean one) and a fallback where a picture is denoised off
 * the GPU. MIT, Lapin's.
 */

/** The B3 spline's taps, centre first. */
export const KERNEL = [3 / 8, 1 / 4, 1 / 16] as const;
/** The passes' strides, pixels. */
export const STEPS = [1, 2, 4, 8, 16] as const;

export interface DenoiseParams {
  /** How sharply a turn of the normal stops the filter (the cosine's power). */
  sigmaN: number;
  /** How far apart in depth two pixels may be, as a share of the distance, per pixel of stride. */
  sigmaZ: number;
  /**
   * How different in light two pixels may be, as the difference of their logarithms (a ratio: the same at any exposure),
   * in units of the picture's own noise there: the spread of the light's logarithm round each pixel (5 × 5), measured.
   */
  sigmaL: number;
  /** The least luminance stop, so a converged (noiseless) picture keeps its edges. */
  floorL: number;
}

/**
 * The parameters. The luminance stop follows the noise the picture has (measured round each pixel), not a guess from its
 * samples: an interior at night is still grainy at 128 samples where a sunlit wall is clean at 16.
 */
export function paramsFor(_samples?: number): DenoiseParams {
  return { sigmaN: 48, sigmaZ: 0.012, sigmaL: 2, floorL: 0.04 };
}

/** The albedo the light is divided by: never below this, so black and the sky keep their own picture. */
export const ALBEDO_FLOOR = 0.03;

export const lum = (r: number, g: number, b: number) => 0.2126 * r + 0.7152 * g + 0.0722 * b;

/**
 * One pass's GLSL: tIllum the light (demodulated), tGuide the normal (xyz, unit) and the view distance (w; 0 where the ray
 * met nothing: the sky, left as it is).
 */
export const ATROUS_FRAGMENT = /* glsl */`
uniform sampler2D tIllum;
uniform sampler2D tGuide;
uniform vec2 texel;
uniform float stride;
uniform float sigmaN;
uniform float sigmaZ;
uniform float sigmaL;
uniform float floorL;
varying vec2 vUv;
float lumOf( vec3 c ) { return dot( c, vec3( 0.2126, 0.7152, 0.0722 ) ); }
void main() {
  vec4 c = texture2D( tIllum, vUv );
  vec4 g = texture2D( tGuide, vUv );
  if ( g.w <= 0.0 ) { gl_FragColor = c; return; }
  float lp = log( max( lumOf( c.rgb ), 1e-5 ) );
  // The luminance stop by the noise measured here (alpha: the spread of log light round the pixel).
  float sl = max( sigmaL * c.a, floorL );
  vec3 sum = vec3( 0.0 );
  float wsum = 0.0;
  float h[3];
  h[0] = 0.375; h[1] = 0.25; h[2] = 0.0625;
  for ( int j = -2; j <= 2; j ++ ) {
    for ( int i = -2; i <= 2; i ++ ) {
      vec2 uv = clamp( vUv + vec2( float( i ), float( j ) ) * texel * stride, texel * 0.5, 1.0 - texel * 0.5 );
      vec4 q = texture2D( tIllum, uv );
      vec4 gq = texture2D( tGuide, uv );
      if ( gq.w <= 0.0 ) continue;
      float k = h[ i < 0 ? -i : i ] * h[ j < 0 ? -j : j ];
      float wn = pow( max( dot( g.xyz, gq.xyz ), 0.0 ), sigmaN );
      float wz = exp( - abs( g.w - gq.w ) / ( sigmaZ * g.w * stride + 1e-4 ) );
      float wl = exp( - abs( lp - log( max( lumOf( q.rgb ), 1e-5 ) ) ) / sl );
      float w = k * wn * wz * wl;
      sum += q.rgb * w;
      wsum += w;
    }
  }
  gl_FragColor = vec4( wsum > 1e-6 ? sum / wsum : c.rgb, c.a );
}`;

/**
 * The light demodulated from the albedo (GLSL): colour / max(albedo, floor); a lone pixel (or a few) far brighter or darker
 * than the 24 round it (a firefly: one path that found a lamp by luck) replaced by their mean, which no edge stop would.
 */
export const DEMODULATE_FRAGMENT = /* glsl */`
uniform sampler2D tColor;
uniform sampler2D tAlbedo;
uniform float floorAlbedo;
uniform vec2 texel;
uniform float spread;
varying vec2 vUv;
vec3 lightAt( vec2 uv ) {
  uv = clamp( uv, texel * 0.5, 1.0 - texel * 0.5 );
  return texture2D( tColor, uv ).rgb / max( texture2D( tAlbedo, uv ).rgb, vec3( floorAlbedo ) );
}
float lumOf( vec3 c ) { return dot( c, vec3( 0.2126, 0.7152, 0.0722 ) ); }
void main() {
  vec3 own = lightAt( vUv );
  vec3 around = vec3( 0.0 );
  for ( int j = -2; j <= 2; j ++ ) for ( int i = -2; i <= 2; i ++ ) if ( i != 0 || j != 0 ) around += lightAt( vUv + vec2( float( i ), float( j ) ) * texel );
  around /= 24.0;
  float lo = lumOf( own ), la = lumOf( around );
  if ( lo > spread * la + 1e-6 || lo * spread < la ) own = around;
  gl_FragColor = vec4( own, 1.0 );
}`;

/**
 * The noise round each pixel (GLSL), on the light with its fireflies out: the spread of the light's logarithm in the
 * quietest of the four 3 × 3 corners of its 5 × 5, so an edge (a shadow's) beside a pixel is not taken for noise. Kept in
 * alpha: the passes' luminance stop.
 */
export const NOISE_FRAGMENT = /* glsl */`
uniform sampler2D tIllum;
uniform vec2 texel;
varying vec2 vUv;
float lumOf( vec3 c ) { return dot( c, vec3( 0.2126, 0.7152, 0.0722 ) ); }
void main() {
  vec4 c = texture2D( tIllum, vUv );
  float L[25];
  for ( int j = -2; j <= 2; j ++ ) for ( int i = -2; i <= 2; i ++ )
    L[ ( j + 2 ) * 5 + i + 2 ] = log( max( lumOf( texture2D( tIllum, clamp( vUv + vec2( float( i ), float( j ) ) * texel, texel * 0.5, 1.0 - texel * 0.5 ) ).rgb ), 1e-5 ) );
  float noise = 1e9;
  for ( int cy = 0; cy <= 2; cy += 2 ) for ( int cx = 0; cx <= 2; cx += 2 ) {
    float s1 = 0.0, s2 = 0.0;
    for ( int j = 0; j < 3; j ++ ) for ( int i = 0; i < 3; i ++ ) { float l = L[ ( cy + j ) * 5 + cx + i ]; s1 += l; s2 += l * l; }
    noise = min( noise, sqrt( max( s2 / 9.0 - ( s1 / 9.0 ) * ( s1 / 9.0 ), 0.0 ) ) );
  }
  gl_FragColor = vec4( c.rgb, noise );
}`;

/** How far a pixel may stray from its neighbours' mean (either way) before it is taken for a firefly. */
export const SPREAD = 4;

export interface Picture {
  width: number;
  height: number;
  /** RGB per pixel, linear light. */
  color: Float32Array;
  /** RGB per pixel. */
  albedo: Float32Array;
  /** XYZ (unit) and the view distance per pixel (0: nothing met). */
  guide: Float32Array;
}

/** The filter on the CPU, exactly as the GPU runs it: demodulate (fireflies out), measure the noise, five passes, remodulate. Returns RGB. */
export function denoiseCpu(p: Picture, params: DenoiseParams): Float32Array {
  const { width: W, height: H, albedo, guide } = p;
  const n = W * H;
  const raw = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) for (let c = 0; c < 3; c++) raw[3 * i + c] = p.color[3 * i + c] / Math.max(albedo[3 * i + c], ALBEDO_FLOOR);
  // Fireflies out, as the shader's demodulation does.
  let src = new Float32Array(n * 3);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const i = y * W + x;
    const m = [0, 0, 0];
    for (let j = -2; j <= 2; j++) for (let k = -2; k <= 2; k++) {
      if (!j && !k) continue;
      const q = Math.min(H - 1, Math.max(0, y + j)) * W + Math.min(W - 1, Math.max(0, x + k));
      for (let c = 0; c < 3; c++) m[c] += raw[3 * q + c] / 24;
    }
    const lo = lum(raw[3 * i], raw[3 * i + 1], raw[3 * i + 2]), la = lum(m[0], m[1], m[2]);
    const fly = lo > SPREAD * la + 1e-6 || lo * SPREAD < la;
    for (let c = 0; c < 3; c++) src[3 * i + c] = fly ? m[c] : raw[3 * i + c];
  }
  // The noise round each pixel, on the light with its fireflies out (as NOISE_FRAGMENT).
  const noise = new Float32Array(n);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const L = new Float64Array(25);
    for (let j = -2; j <= 2; j++) for (let k = -2; k <= 2; k++) {
      const q = Math.min(H - 1, Math.max(0, y + j)) * W + Math.min(W - 1, Math.max(0, x + k));
      L[(j + 2) * 5 + k + 2] = Math.log(Math.max(1e-5, lum(src[3 * q], src[3 * q + 1], src[3 * q + 2])));
    }
    let quiet = Infinity;
    for (const cy of [0, 2]) for (const cx of [0, 2]) {
      let s1 = 0, s2 = 0;
      for (let j = 0; j < 3; j++) for (let k = 0; k < 3; k++) { const l = L[(cy + j) * 5 + cx + k]; s1 += l; s2 += l * l; }
      quiet = Math.min(quiet, Math.sqrt(Math.max(0, s2 / 9 - (s1 / 9) ** 2)));
    }
    noise[y * W + x] = quiet;
  }
  let dst = new Float32Array(n * 3);
  const logLum = (a: Float32Array, i: number) => Math.log(Math.max(1e-5, lum(a[3 * i], a[3 * i + 1], a[3 * i + 2])));
  for (const stride of STEPS) {
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x;
      const gw = guide[4 * i + 3];
      if (gw <= 0) { dst[3 * i] = src[3 * i]; dst[3 * i + 1] = src[3 * i + 1]; dst[3 * i + 2] = src[3 * i + 2]; continue; }
      const lp = logLum(src, i);
      const sl = Math.max(params.sigmaL * noise[i], params.floorL);
      let r = 0, g = 0, b = 0, ws = 0;
      for (let j = -2; j <= 2; j++) for (let k = -2; k <= 2; k++) {
        const qx = Math.min(W - 1, Math.max(0, x + k * stride)), qy = Math.min(H - 1, Math.max(0, y + j * stride));
        const q = qy * W + qx;
        const qw = guide[4 * q + 3];
        if (qw <= 0) continue;
        const kern = KERNEL[Math.abs(k)] * KERNEL[Math.abs(j)];
        const dot = guide[4 * i] * guide[4 * q] + guide[4 * i + 1] * guide[4 * q + 1] + guide[4 * i + 2] * guide[4 * q + 2];
        const wn = Math.pow(Math.max(0, dot), params.sigmaN);
        const wz = Math.exp(-Math.abs(gw - qw) / (params.sigmaZ * gw * stride + 1e-4));
        const wl = Math.exp(-Math.abs(lp - logLum(src, q)) / sl);
        const w = kern * wn * wz * wl;
        r += src[3 * q] * w; g += src[3 * q + 1] * w; b += src[3 * q + 2] * w; ws += w;
      }
      if (ws > 1e-6) { dst[3 * i] = r / ws; dst[3 * i + 1] = g / ws; dst[3 * i + 2] = b / ws; }
      else { dst[3 * i] = src[3 * i]; dst[3 * i + 1] = src[3 * i + 1]; dst[3 * i + 2] = src[3 * i + 2]; }
    }
    [src, dst] = [dst, src];
  }
  const out = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) for (let c = 0; c < 3; c++) out[3 * i + c] = src[3 * i + c] * Math.max(albedo[3 * i + c], ALBEDO_FLOOR);
  return out;
}

/** The mean squared error between two RGB pictures. */
export function mse(a: Float32Array, b: Float32Array): number {
  let s = 0;
  for (let i = 0; i < a.length; i++) s += (a[i] - b[i]) ** 2;
  return s / a.length;
}
