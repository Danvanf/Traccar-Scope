export default function TelemetryGraphLoading({ embedded = false, publicMode = false, title = 'Trip Telemetry Graph', onClose }) {
  return <div
    className={embedded ? 'telemetry-graph-embedded' : publicMode ? 'telemetry-graph-inline' : 'telemetry-graph-modal'}
    role={embedded || publicMode ? undefined : 'dialog'}
    aria-modal={embedded || publicMode ? undefined : 'true'}
    aria-label={embedded || publicMode ? undefined : title}
  >
    <div className="telemetry-graph-loading" role="status" aria-live="polite" aria-atomic="true" aria-busy="true">
      <span className="telemetry-graph-loading-spinner" aria-hidden="true" />
      <strong>Loading trip telemetry…</strong>
      <p>Please wait while the data is loaded and the graph is prepared.</p>
      {!embedded && !publicMode && <button type="button" className="secondary" onClick={onClose}>Cancel</button>}
    </div>
  </div>
}
