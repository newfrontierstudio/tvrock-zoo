// Quantum cognition v2 engine in Rust: a line-for-line port of NfsCore's
// QuantumCognition / QuantumMath (TsRecommend), so numbers can be checked
// against the C# implementation on identical input.
// Usage: quantum-core-rs <fixture.json>  (same JSON shape the C# harness emits)
// Output: JSON array of evaluations, one per query.
use chrono::{DateTime, FixedOffset};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::io::{BufRead, Write};
use std::path::Path;
use unicode_normalization::UnicodeNormalization;

#[derive(Clone, Copy, Default)]
struct QDensity { x: f64, y: f64, z: f64 }
#[derive(Clone, Copy)]
struct QAxis { x: f64, y: f64, z: f64 }
const INTEREST: QAxis = QAxis { x: 0.0, y: 0.0, z: 1.0 };
const ALLOCATION: QAxis = QAxis { x: 1.0, y: 0.0, z: 0.0 };

impl QDensity {
    fn prior() -> Self { QDensity { x: 0.75f64.sqrt(), y: 0.0, z: 0.5 } }
    fn probability(&self, n: QAxis) -> f64 { ((1.0 + self.x * n.x + self.y * n.y + self.z * n.z) / 2.0).clamp(0.0, 1.0) }
    fn observe(&self, n: QAxis, outcome: i32, strength: f64) -> Self {
        let eta = strength.clamp(0.0, 0.95);
        let v = self.x * n.x + self.y * n.y + self.z * n.z;
        let den = 1.0 + outcome as f64 * eta * v;
        let k = (1.0 - eta * eta).sqrt();
        QDensity {
            x: (k * (self.x - v * n.x) + (v + outcome as f64 * eta) * n.x) / den,
            y: (k * (self.y - v * n.y) + (v + outcome as f64 * eta) * n.y) / den,
            z: (k * (self.z - v * n.z) + (v + outcome as f64 * eta) * n.z) / den,
        }
    }
    fn precess(&self, phase: f64) -> Self { let (c, s) = (phase.cos(), phase.sin()); QDensity { x: self.x * c - self.y * s, y: self.x * s + self.y * c, z: self.z } }
    fn rotate_y(&self, angle: f64) -> Self { let (c, s) = (angle.cos(), angle.sin()); QDensity { x: self.x * c + self.z * s, y: self.y, z: -self.x * s + self.z * c } }
    fn dephase(&self, coherence: f64) -> Self { let k = coherence.clamp(0.0, 1.0); QDensity { x: self.x * k, y: self.y * k, z: self.z } }
}

#[derive(Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Booking {
    id: String, title: String, #[allow(dead_code)] station: String, channel_key: String,
    start: String, end: String, start_adjustment: i64, state: String,
    user_selected: bool, user_cancelled: bool,
    user_action_at: Option<String>, user_cancelled_at: Option<String>, observed_at: Option<String>,
    cancellation_context: String, recommendation_key: String, recommendation_created_at: Option<String>,
    #[allow(dead_code)] minimum_duration_suppressed: bool, #[allow(dead_code)] registration_origin: String,
    #[allow(dead_code)] import_source_key: String, #[allow(dead_code)] search_rule_id: String,
}
#[derive(Clone)]
struct TB { b: Booking, start: DateTime<FixedOffset>, end: DateTime<FixedOffset> }
fn parse(s: &str) -> DateTime<FixedOffset> { DateTime::parse_from_rfc3339(s).expect("timestamp") }
fn opt(s: &Option<String>) -> Option<DateTime<FixedOffset>> { s.as_deref().map(parse) }

impl TB {
    fn negative(&self) -> bool { (self.b.user_cancelled || self.b.state == "Cancelled") && self.b.cancellation_context != "stop-recording" }
    fn positive(&self) -> bool { !self.b.user_cancelled && !matches!(self.b.state.as_str(), "Cancelled" | "Disabled" | "Failed") && (self.b.user_selected || self.b.recommendation_key.is_empty()) }
    fn stamp(&self) -> Option<DateTime<FixedOffset>> { if self.negative() { opt(&self.b.user_cancelled_at) } else { opt(&self.b.user_action_at) } }
    fn event_date(&self) -> DateTime<FixedOffset> {
        if let Some(s) = self.stamp() { return s; }
        if self.negative() { if let Some(o) = opt(&self.b.observed_at) { return o; } }
        if let Some(r) = opt(&self.b.recommendation_created_at) { return r; }
        self.start
    }
}
fn overlap(a: DateTime<FixedOffset>, b: DateTime<FixedOffset>, c: DateTime<FixedOffset>, d: DateTime<FixedOffset>) -> bool { a < d && b > c }

// ---- Series normalization (mirror of CognitionLearning.Series / Data.Norm) ----
fn timing_series(title: &str) -> String {
    let cuts = ['【', '「', '▽', '▼', '～', '〜', '—', '：'];
    let mut cut: i64 = title.char_indices().find(|(_, c)| cuts.contains(c)).map(|(i, _)| i as i64).unwrap_or(-1);
    if let Some(space) = title.find(' ') {
        let head = &title[..space];
        let has_kana = head.chars().any(|c| ('\u{3040}'..='\u{30ff}').contains(&c) || ('\u{4e00}'..='\u{9fff}').contains(&c));
        if space > 0 && has_kana && (cut < 0 || (space as i64) < cut) { cut = space as i64; }
    }
    let out = if cut > 0 { &title[..cut as usize] } else { title };
    out.trim().to_string()
}
fn series_text(title: &str) -> String {
    // remove [...] and ［...］ spans
    let mut text = String::new(); let mut depth: i32 = 0;
    for c in title.chars() {
        match c { '[' | '［' => depth += 1, ']' | '］' => depth = depth.saturating_sub(1), _ if depth == 0 => text.push(c), _ => {} }
    }
    // strip from the first 第N話/回 or #N to the end
    let chars: Vec<(usize, char)> = text.char_indices().collect();
    let mut cut: Option<usize> = None;
    for w in 0..chars.len() {
        let (i, c) = chars[w];
        if c == '第' || c == '#' || c == '＃' {
            let mut j = w + 1;
            if c == '第' {
                while j < chars.len() && chars[j].1.is_whitespace() { j += 1; }
                let digits = j;
                while j < chars.len() && chars[j].1.is_ascii_digit() { j += 1; }
                if j > digits {
                    while j < chars.len() && chars[j].1.is_whitespace() { j += 1; }
                    if j < chars.len() && (chars[j].1 == '話' || chars[j].1 == '回') { cut = Some(i); break; }
                }
            } else {
                while j < chars.len() && chars[j].1.is_whitespace() { j += 1; }
                if j < chars.len() && chars[j].1.is_ascii_digit() { cut = Some(i); break; }
            }
        }
    }
    if let Some(i) = cut { text.truncate(i); }
    timing_series(&text)
}
fn norm(s: &str) -> String {
    let cleaned: String = s.nfkc().collect();
    let mut collapsed = String::new(); let mut ws = false;
    for c in cleaned.chars() { if c.is_whitespace() { ws = true; } else { if ws && !collapsed.is_empty() { collapsed.push(' '); } ws = false; collapsed.push(c); } }
    let mut t = collapsed.trim().to_string();
    for tag in ["[再]", "[字]", "[デ]", "[解]", "[二]", "[多]", "[新]", "[終]", "[無料]"] { t = t.replace(tag, ""); }
    // C# removes [\s\p{P}\p{S}]; approximation: keep alphanumeric characters.
    let kept: String = t.chars().filter(|c| c.is_alphanumeric()).collect();
    kept.to_lowercase()
}
fn series(title: &str) -> String { norm(&series_text(title)) }

// ---- Engine ----
#[derive(Clone, Copy, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Options { enabled: bool, weight: f64, eta_scale: f64, half_life_days: f64, precession_days: f64, decoherence_half_life_days: f64, busy_rotation_radians: f64, rivalry_boost: f64 }
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Query { key: String, title: String, #[allow(dead_code)] station: String, start: String, end: String, classical_factor: f64 }
#[derive(Deserialize)]
struct Fixture { now: String, options: Options, history: Vec<Booking>, queries: Vec<Query> }

struct Engine { history: Vec<TB>, groups: HashMap<String, Vec<TB>>, now: DateTime<FixedOffset>, options: Options, states: HashMap<String, (QDensity, f64, i64, i64, i64)>, positives: Vec<usize> }
impl Engine {
    fn weight(&self, b: &TB) -> f64 {
        let days = ((self.now - b.event_date()).num_milliseconds() as f64 / 86400000.0).max(0.0);
        if days > 180.0 { 0.0 } else { 0.5f64.powf(days / self.options.half_life_days) }
    }
    // Positives are precomputed once; the scan early-exits on first overlap.
    fn competing(&self, b: &TB) -> bool { self.positives.iter().any(|&i| { let o = &self.history[i]; o.b.id != b.b.id && overlap(b.start, b.end, o.start, o.end) }) }
    fn state(&mut self, key: &str) -> (QDensity, f64, i64, i64, i64) {
        if let Some(v) = self.states.get(key) { return *v; }
        let relevant: Vec<TB> = self.groups.get(key).cloned().unwrap_or_default().into_iter().filter(|b| b.positive() || b.negative()).collect();
        let (mut x, mut z, mut total, mut evidence) = (QDensity::prior().x * 4.0, QDensity::prior().z * 4.0, 4.0f64, 0.0f64);
        let (mut legacy, mut ordered, mut rival) = (0i64, 0i64, 0i64);
        let mut latest: Option<DateTime<FixedOffset>> = None;
        let touch = |b: &TB, latest: &mut Option<DateTime<FixedOffset>>| { let d = b.event_date(); if latest.map_or(true, |l| d > l) { *latest = Some(d); } };
        for b in relevant.iter().filter(|b| b.stamp().is_none()) {
            let w = self.weight(b); if w <= 0.0 { continue; }
            touch(b, &mut latest);
            let context = b.negative() && self.competing(b);
            if context { x += w; evidence += 0.35 * w; rival += 1; } else { z += if b.positive() { w } else { -w }; evidence += w; }
            total += w; legacy += 1;
        }
        let mut state = QDensity { x: x / total, y: 0.0, z: z / total };
        let mut timed: Vec<&TB> = relevant.iter().filter(|b| b.stamp().map_or(false, |s| s <= self.now)).collect();
        timed.sort_by(|a, b| a.stamp().cmp(&b.stamp()).then(a.b.id.cmp(&b.b.id)));
        if timed.len() > 64 { timed.drain(..timed.len() - 64); }
        let mut previous: Option<DateTime<FixedOffset>> = None;
        for b in timed {
            let w = self.weight(b); if w <= 0.0 { continue; }
            touch(b, &mut latest);
            let stamp = b.stamp().unwrap();
            if let Some(p) = previous { state = state.precess(2.0 * std::f64::consts::PI * (stamp - p).num_milliseconds() as f64 / 86400000.0 / self.options.precession_days); }
            previous = Some(stamp);
            let context = b.negative() && self.competing(b);
            let eta = (if context { 0.12 } else if b.positive() { 0.12 } else { 0.20 }) * self.options.eta_scale * w;
            state = state.observe(if context { ALLOCATION } else { INTEREST }, if context || b.positive() { 1 } else { -1 }, eta);
            evidence += w * if context { 0.35 } else { 1.0 }; ordered += 1; if context { rival += 1; }
        }
        if let Some(l) = latest { let idle = ((self.now - l).num_milliseconds() as f64 / 86400000.0).max(0.0); state = state.dephase(0.5f64.powf(idle / self.options.decoherence_half_life_days)); }
        let value = (state, evidence, ordered, legacy, rival);
        self.states.insert(key.to_string(), value);
        value
    }
    fn evaluate(&mut self, q: &Query) -> serde_json::Value {
        let key = series(&q.title);
        let prior = self.state(&key);
        let (qs, qe) = (parse(&q.start), parse(&q.end));
        let busy = self.positives.iter().any(|&i| { let b = &self.history[i]; b.b.id != q.key && overlap(b.start, b.end, qs, qe) });
        let mut state = prior.0;
        let mut dephased = QDensity { x: 0.0, y: 0.0, z: state.z };
        if busy { state = state.rotate_y(self.options.busy_rotation_radians); dephased = dephased.rotate_y(self.options.busy_rotation_radians); }
        let probability = state.probability(INTEREST);
        let without = dephased.probability(INTEREST);
        let mut confidence = prior.1 / (prior.1 + 2.0);
        if prior.1 == 0.0 && busy { confidence = 0.2; }
        let mut quantum = (1.0 + confidence * (probability / 0.75 - 1.0)).clamp(0.45, 1.3);
        if busy && prior.4 > 0 && quantum < 1.0 { quantum = (1.0 + (quantum - 1.0) * (1.0 + self.options.rivalry_boost * prior.4.min(3) as f64)).clamp(0.45, 1.3); }
        let factor = if self.options.enabled { (self.options.weight * quantum + (1.0 - self.options.weight) * q.classical_factor).clamp(0.45, 1.3) } else { q.classical_factor };
        serde_json::json!({"key": q.key, "probability": probability, "dephasedProbability": without, "quantumFactor": quantum, "factor": factor, "orderedEvents": prior.2, "legacyEvents": prior.3, "busy": busy})
    }
}

fn build_engine(bookings: &[Booking], now: DateTime<FixedOffset>, options: Options) -> Engine {
    // Dedupe like the C# engine: group by (channel, adjusted start), last positive wins, else last.
    let mut order: Vec<(String, i64)> = Vec::new();
    let mut slots: HashMap<(String, i64), (Option<TB>, TB)> = HashMap::new();
    for b in bookings {
        let tb = TB { b: b.clone(), start: parse(&b.start), end: parse(&b.end) };
        let key = (b.channel_key.clone(), tb.start.timestamp_nanos_opt().unwrap() - b.start_adjustment as i64 * 1_000_000_000);
        let entry = slots.entry(key.clone()).or_insert_with(|| { order.push(key.clone()); (None, tb.clone()) });
        if tb.positive() { entry.0 = Some(tb.clone()); }
        entry.1 = tb;
    }
    let history: Vec<TB> = order.iter().map(|k| { let (p, l) = &slots[k]; p.clone().unwrap_or_else(|| l.clone()) }).collect();
    let mut groups: HashMap<String, Vec<TB>> = HashMap::new();
    for b in &history { groups.entry(series(&b.b.title)).or_default().push(b.clone()); }
    let positives: Vec<usize> = history.iter().enumerate().filter(|(_, b)| b.positive()).map(|(i, _)| i).collect();
    Engine { history, groups, now, options, states: HashMap::new(), positives }
}

// ---- D: QuantumCalibrator port (leave-one-out log-loss grid search) ----
#[derive(Clone, Copy, Serialize)]
#[serde(rename_all = "PascalCase")]
struct Calibration { eta_scale: f64, half_life_days: f64, log_loss: f64, samples: i64,
    #[serde(default = "default_precession")] precession_days: f64,
    #[serde(default = "default_decoherence")] decoherence_half_life_days: f64 }

// Mirrors QuantumCalibrator.ExtractEvents: timed positive/negative events
// up to now, oldest first, capped at the latest 120.
fn extract_events<'a>(bookings: &'a [Booking], now: DateTime<FixedOffset>) -> Vec<&'a Booking> {
    let mut events: Vec<&Booking> = bookings.iter().filter(|b| {
        let tb = TB { b: (*b).clone(), start: parse(&b.start), end: parse(&b.end) };
        (tb.positive() || tb.negative()) && tb.stamp().map_or(false, |s| s <= now)
    }).collect();
    events.sort_by(|a, b| {
        let ta = TB { b: (*a).clone(), start: parse(&a.start), end: parse(&a.end) };
        let tb = TB { b: (*b).clone(), start: parse(&b.start), end: parse(&b.end) };
        ta.stamp().cmp(&tb.stamp()).then(a.id.cmp(&b.id))
    });
    if events.len() > 120 { events.drain(..events.len() - 120); }
    events
}

// Mirrors QuantumCalibrator.MeanLogLoss over a fixed option set.
// Events are independent leave-one-out problems: they are evaluated in
// parallel across threads, but the per-event losses are summed
// sequentially in event order, so the result is bit-identical.
fn event_loss(bookings: &[Booking], e: &Booking, options: Options) -> f64 {
    let tb_e = TB { b: e.clone(), start: parse(&e.start), end: parse(&e.end) };
    let stamp = tb_e.stamp().unwrap();
    let rest: Vec<Booking> = bookings.iter().filter(|b| {let t=TB{b:(*b).clone(),start:parse(&b.start),end:parse(&b.end)};b.id != e.id && t.stamp().map_or(true,|s|s<stamp)}).cloned().collect();
    let mut engine = build_engine(&rest, stamp, options);
    let state = engine.state(&series(&e.title));
    let context = tb_e.negative() && engine.competing(&tb_e);
    let p = state.0.probability(if context { ALLOCATION } else { INTEREST });
    let observed = if context || tb_e.positive() { p } else { 1.0 - p };
    -observed.clamp(0.02, 0.98).ln()
}
fn mean_log_loss(bookings: &[Booking], events: &[&Booking], options: Options) -> f64 {
    let n = events.len();
    let mut losses = vec![0.0f64; n];
    let threads = std::thread::available_parallelism().map(|v| v.get()).unwrap_or(1).min(n.max(1));
    if threads <= 1 {
        for (i, e) in events.iter().enumerate() { losses[i] = event_loss(bookings, e, options); }
    } else {
        let chunk = (n + threads - 1) / threads;
        std::thread::scope(|s| {
            for (ci, part) in losses.chunks_mut(chunk).enumerate() {
                let base = ci * chunk;
                s.spawn(move || {
                    for (j, slot) in part.iter_mut().enumerate() { *slot = event_loss(bookings, events[base + j], options); }
                });
            }
        });
    }
    losses.iter().sum::<f64>() / n as f64
}

fn calibrate(bookings: &[Booking], now: DateTime<FixedOffset>, baseline: &Options) -> Option<Calibration> {
    let events = extract_events(bookings, now);
    if events.len() < 8 { return None; }
    let mut best: Option<Calibration> = None;
    for eta in [0.7f64, 1.0, 1.4] { for half in [30f64, 45.0, 70.0] {
        let options = Options { enabled: true, weight: baseline.weight, eta_scale: eta, half_life_days: half,
            precession_days: baseline.precession_days, decoherence_half_life_days: baseline.decoherence_half_life_days,
            busy_rotation_radians: baseline.busy_rotation_radians, rivalry_boost: baseline.rivalry_boost };
        let mean = mean_log_loss(bookings, &events, options);
        if best.map_or(true, |b| mean < b.log_loss) { best = Some(Calibration { eta_scale: eta, half_life_days: half, log_loss: mean, samples: events.len() as i64, precession_days: 21.0, decoherence_half_life_days: 60.0 }); }
    } }
    best
}

// Mirrors QuantumCalibrator.CalibrateChaos: a deterministic logistic map
// (r=3.99, seed derived from the history) explores 24 candidate option
// sets over EtaScale / HalfLifeDays / PrecessionDays /
// DecoherenceHalfLifeDays; the lowest leave-one-out log-loss wins.
// Adoption stays guarded on the C# side (ChaosComponent).
fn utc_ticks(start: &str) -> i64 {
    // .NET DateTimeOffset.UtcTicks: 100ns units since 0001-01-01.
    parse(start).timestamp_nanos_opt().unwrap() / 100 + 621_355_968_000_000_000
}
fn calibrate_chaos(bookings: &[Booking], now: DateTime<FixedOffset>, baseline: &Options) -> Option<Calibration> {
    let events = extract_events(bookings, now);
    if events.len() < 8 { return None; }
    let tick_sum: i64 = events.iter().map(|e| utc_ticks(&e.start) % 997).sum();
    let mut x = ((events.len() as f64 * 0.013 + tick_sum as f64 * 0.000001) % 0.9) + 0.05;
    let mut best: Option<Calibration> = None;
    for _ in 0..24 {
        x = 3.99 * x * (1.0 - x); let eta = 0.55 + 1.2 * x;
        x = 3.99 * x * (1.0 - x); let half = 14.0 + 106.0 * x;
        x = 3.99 * x * (1.0 - x); let precession = 5.0 + 85.0 * x;
        x = 3.99 * x * (1.0 - x); let decoherence = 14.0 + 166.0 * x;
        let options = Options { enabled: true, weight: baseline.weight, eta_scale: eta, half_life_days: half,
            precession_days: precession, decoherence_half_life_days: decoherence,
            busy_rotation_radians: baseline.busy_rotation_radians, rivalry_boost: baseline.rivalry_boost };
        let loss = mean_log_loss(bookings, &events, options);
        if best.map_or(true, |b| loss < b.log_loss) { best = Some(Calibration { eta_scale: eta, half_life_days: half, log_loss: loss, samples: events.len() as i64, precession_days: precession, decoherence_half_life_days: decoherence }); }
    }
    best
}

// ---- QuantumCognitionOptions file I/O port (quantum-options.json) ----
// Mirrors the C# Load/Save: PascalCase JSON, Weight validated by error,
// every other parameter clamped to its range (non-finite -> default).
#[derive(Clone, Copy, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase", default)]
struct OptionsFile { enabled: bool, weight: f64, eta_scale: f64, half_life_days: f64, precession_days: f64, decoherence_half_life_days: f64, busy_rotation_radians: f64, rivalry_boost: f64 }
impl Default for OptionsFile {
    fn default() -> Self { OptionsFile { enabled: true, weight: 0.70, eta_scale: 1.0, half_life_days: 45.0, precession_days: 21.0, decoherence_half_life_days: 60.0, busy_rotation_radians: 0.30, rivalry_boost: 0.15 } }
}
impl OptionsFile {
    fn engine(&self) -> Options { Options { enabled: self.enabled, weight: self.weight, eta_scale: self.eta_scale, half_life_days: self.half_life_days, precession_days: self.precession_days, decoherence_half_life_days: self.decoherence_half_life_days, busy_rotation_radians: self.busy_rotation_radians, rivalry_boost: self.rivalry_boost } }
    fn validate_weight(&self, message: &str) -> Result<(), String> { if !self.weight.is_finite() || !(0.0..=1.0).contains(&self.weight) { return Err(message.to_string()); } Ok(()) }
    fn clamped(mut self) -> Self {
        let cl = |v: f64, lo: f64, hi: f64, d: f64| if v.is_finite() { v.clamp(lo, hi) } else { d };
        self.eta_scale = cl(self.eta_scale, 0.5, 2.0, 1.0);
        self.half_life_days = cl(self.half_life_days, 7.0, 180.0, 45.0);
        self.precession_days = cl(self.precession_days, 3.0, 180.0, 21.0);
        self.decoherence_half_life_days = cl(self.decoherence_half_life_days, 7.0, 365.0, 60.0);
        self.busy_rotation_radians = cl(self.busy_rotation_radians, 0.0, 1.0, 0.30);
        self.rivalry_boost = cl(self.rivalry_boost, 0.0, 0.5, 0.15);
        self
    }
}
fn options_path(home: &str) -> std::path::PathBuf { Path::new(home).join("quantum-options.json") }
fn load_options(home: &str) -> Result<OptionsFile, String> {
    let path = options_path(home);
    let parsed: OptionsFile = match std::fs::read_to_string(&path) {
        Ok(text) => { let t = text.trim(); if t == "null" { OptionsFile::default() } else { serde_json::from_str(&text).map_err(|e| e.to_string())? } }
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => OptionsFile::default(),
        Err(e) => return Err(e.to_string()),
    };
    parsed.validate_weight("量子認知の反映率は0〜100%で指定してください。")?;
    Ok(parsed.clamped())
}
fn durable_write(path: &Path, text: &str) -> Result<(), String> {
    if let Some(parent) = path.parent() { std::fs::create_dir_all(parent).map_err(|e| e.to_string())?; }
    let tmp = path.with_extension(format!("{}.tmp", std::process::id()));
    {
        let mut f = std::fs::File::create(&tmp).map_err(|e| e.to_string())?;
        f.write_all(text.as_bytes()).map_err(|e| e.to_string())?;
        f.sync_all().map_err(|e| e.to_string())?;
    }
    let bak = path.with_extension("json.bak");
    if path.exists() {
        if bak.exists() { let _ = std::fs::remove_file(&bak); }
        std::fs::rename(path, &bak).map_err(|e| e.to_string())?;
    }
    std::fs::rename(&tmp, path).map_err(|e| { let _ = std::fs::remove_file(&tmp); e.to_string() })
}
fn save_options(home: &str, options: &OptionsFile) -> Result<(), String> {
    options.validate_weight("量子認知の反映率を確認してください。")?;
    let text = serde_json::to_string(options).map_err(|e| e.to_string())?;
    durable_write(&options_path(home), &text)
}

// ---- Worker mode: newline-delimited JSON on stdin/stdout ----
// {"type":"ping"} {"type":"init",now,options,history} {"type":"evaluate",query}
// {"type":"calibrate",baseline[,now,history]} {"type":"calibrateChaos",baseline[,now,history]}
// {"type":"loadOptions",home}
// {"type":"saveOptions",home,options}   (options in the PascalCase file shape)
fn worker() {
    let stdin = std::io::stdin();
    let mut out = std::io::stdout().lock();
    let mut session: Option<(Engine, Vec<Booking>, DateTime<FixedOffset>)> = None;
    for line in stdin.lock().lines() {
        let Ok(line) = line else { break };
        let line = line.trim();
        if line.is_empty() { continue; }
        let response = dispatch(&mut session, line);
        let _ = writeln!(out, "{response}");
        let _ = out.flush();
    }
}
fn dispatch(session: &mut Option<(Engine, Vec<Booking>, DateTime<FixedOffset>)>, line: &str) -> String {
    let v: serde_json::Value = match serde_json::from_str(line) { Ok(v) => v, Err(e) => return error_json(&e.to_string()) };
    let kind = v.get("type").and_then(|t| t.as_str()).unwrap_or("");
    let result: Result<serde_json::Value, String> = (|| {
        match kind {
            "ping" => Ok(serde_json::json!({"type":"pong","engine":"rust","algorithm":"quantum-cognition-v3-chaos"})),
            "init" => {
                let now = parse(v.get("now").and_then(|x| x.as_str()).ok_or("now")?);
                let options: Options = serde_json::from_value(v.get("options").cloned().ok_or("options")?).map_err(|e| e.to_string())?;
                let history: Vec<Booking> = serde_json::from_value(v.get("history").cloned().ok_or("history")?).map_err(|e| e.to_string())?;
                let engine = build_engine(&history, now, options);
                let count = engine.history.len();
                *session = Some((engine, history, now));
                Ok(serde_json::json!({"type":"ready","bookings":count}))
            }
            "evaluate" => {
                let (engine, _, _) = session.as_mut().ok_or("not initialized")?;
                let query: Query = serde_json::from_value(v.get("query").cloned().ok_or("query")?).map_err(|e| e.to_string())?;
                let mut r = engine.evaluate(&query);
                r["type"] = serde_json::json!("evaluation");
                r["classicalFactor"] = serde_json::json!(query.classical_factor);
                Ok(r)
            }
            "calibrate" => {
                let baseline_file: OptionsFile = serde_json::from_value(v.get("baseline").cloned().ok_or("baseline")?).map_err(|e| e.to_string())?;
                let (history, now) = match v.get("history") {
                    Some(h) => (serde_json::from_value::<Vec<Booking>>(h.clone()).map_err(|e| e.to_string())?,
                                v.get("now").and_then(|x| x.as_str()).map(parse).ok_or("now")?),
                    None => { let (_, h, n) = session.as_ref().ok_or("not initialized")?; (h.clone(), *n) }
                };
                let calibration = calibrate(&history, now, &baseline_file.engine());
                Ok(serde_json::json!({"type":"calibration","calibration":calibration}))
            }
            "calibrateChaos" => {
                let baseline_file: OptionsFile = serde_json::from_value(v.get("baseline").cloned().ok_or("baseline")?).map_err(|e| e.to_string())?;
                let (history, now) = match v.get("history") {
                    Some(h) => (serde_json::from_value::<Vec<Booking>>(h.clone()).map_err(|e| e.to_string())?,
                                v.get("now").and_then(|x| x.as_str()).map(parse).ok_or("now")?),
                    None => { let (_, h, n) = session.as_ref().ok_or("not initialized")?; (h.clone(), *n) }
                };
                let calibration = calibrate_chaos(&history, now, &baseline_file.engine());
                Ok(serde_json::json!({"type":"chaosCalibration","calibration":calibration}))
            }
            "loadOptions" => {
                let home = v.get("home").and_then(|x| x.as_str()).ok_or("home")?;
                let options = load_options(home)?;
                Ok(serde_json::json!({"type":"options","options":options}))
            }
            "saveOptions" => {
                let home = v.get("home").and_then(|x| x.as_str()).ok_or("home")?;
                let options: OptionsFile = serde_json::from_value(v.get("options").cloned().ok_or("options")?).map_err(|e| e.to_string())?;
                save_options(home, &options)?;
                Ok(serde_json::json!({"type":"saved"}))
            }
            _ => Err(format!("unknown type: {kind}")),
        }
    })();
    match result { Ok(v) => v.to_string(), Err(m) => error_json(&m) }
}
fn error_json(message: &str) -> String { serde_json::json!({"type":"error","message":message}).to_string() }

fn main() {
    let arg = std::env::args().nth(1).expect("usage: quantum-core-rs <fixture.json> | worker");
    if arg == "worker" { worker(); return; }
    let fixture: Fixture = serde_json::from_str(&std::fs::read_to_string(&arg).expect("read fixture")).expect("parse fixture");
    let now = parse(&fixture.now);
    let mut engine = build_engine(&fixture.history, now, fixture.options);
    let results: Vec<serde_json::Value> = fixture.queries.iter().map(|q| engine.evaluate(q)).collect();
    let calibration = calibrate(&fixture.history, now, &fixture.options);
    let chaos_calibration = calibrate_chaos(&fixture.history, now, &fixture.options);
    println!("{}", serde_json::to_string_pretty(&serde_json::json!({"results":results,"calibration":calibration,"chaosCalibration":chaos_calibration})).unwrap());
}
