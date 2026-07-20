import { useState, useEffect } from 'react';
import { supabase } from './supabase';
import { Cloud, Plus, Trash2, Shield, DollarSign, FileCode, CheckCircle2, ShieldAlert } from 'lucide-react';

export default function UpdateManager({ isDarkMode: _isDarkMode }: { isDarkMode: boolean }) {
  const [updates, setUpdates] = useState<any[]>([]);
  const [version, setVersion] = useState('');
  const [updateType, setUpdateType] = useState<'minor' | 'major'>('minor');
  const [isCompulsory, setIsCompulsory] = useState(false);
  const [changelog, setChangelog] = useState('');
  const [paymentAmount, setPaymentAmount] = useState('0.00');
  const [fileUrl, setFileUrl] = useState('');
  const [isPublishing, setIsPublishing] = useState(false);
  const [githubReleases, setGithubReleases] = useState<any[]>([]);
  const [isFetchingGithub, setIsFetchingGithub] = useState(false);

  useEffect(() => {
    fetchUpdates();
  }, []);

  const fetchUpdates = async () => {
    const { data, error } = await supabase.from('app_updates')
      .select('*')
      .order('created_at', { ascending: false });
    
    if (!error && data) {
      setUpdates(data);
    }
  };

  const publishUpdate = async () => {
    if (!version || !fileUrl) {
      return alert('Version code and File Download URL are required.');
    }

    // Basic version syntax check (e.g. 1.0.0)
    const versionRegex = /^\d+\.\d+\.\d+$/;
    if (!versionRegex.test(version)) {
      return alert('Invalid version format. Please use standard semantic versioning (e.g. 1.1.0 or 2.0.0).');
    }

    const price = parseFloat(paymentAmount) || 0.00;
    if (updateType === 'major' && price <= 0) {
      return alert('Major upgrades require a premium payment amount greater than $0.00.');
    }

    setIsPublishing(true);

    const payload = {
      version,
      update_type: updateType,
      is_compulsory: isCompulsory,
      changelog,
      payment_amount: updateType === 'major' ? price : 0.00,
      file_url: fileUrl
    };

    const { error } = await supabase.from('app_updates').insert([payload]);

    if (error) {
      alert('Error publishing update: ' + error.message);
    } else {
      setVersion('');
      setUpdateType('minor');
      setIsCompulsory(false);
      setChangelog('');
      setPaymentAmount('0.00');
      setFileUrl('');
      fetchUpdates();
      alert('Software update successfully published to update server!');
    }
    setIsPublishing(false);
  };

  const deleteUpdate = async (id: string, versionString: string) => {
    if (!window.confirm(`Revoke and delete Version ${versionString} from the update server?`)) return;
    const { error } = await supabase.from('app_updates').delete().eq('id', id);
    if (!error) fetchUpdates();
  };

  const fetchGithubReleases = async () => {
    setIsFetchingGithub(true);
    try {
      const res = await fetch('https://api.github.com/repos/albertantony308/JobOrderGenerator/releases');
      if (res.ok) {
        const data = await res.json();
        setGithubReleases(data);
      } else {
        alert('Failed to fetch from GitHub: ' + res.statusText);
      }
    } catch (e: any) {
      alert('Error fetching from GitHub: ' + e.message);
    }
    setIsFetchingGithub(false);
  };

  const autofillFromGithub = (release: any) => {
    const v = release.tag_name.replace(/^v/, '');
    setVersion(v);
    setChangelog(release.body || '');
    if (release.assets && release.assets.length > 0) {
      // Find the asset that IS an Inno Setup installer
      const setupAsset = release.assets.find((a: any) => a.name.toLowerCase().includes('_setup_'));
      setFileUrl(setupAsset ? setupAsset.browser_download_url : release.assets[0].browser_download_url);
    }
  };

  return (
    <div className="dashboard-area" style={{ width: '100%', overflowY: 'auto', padding: '60px 40px' }}>
      <div style={{ maxWidth: 1200, margin: '0 auto' }}>
        <h1 className="headline-lg" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Cloud /> Application Update Dashboard
        </h1>
        <p className="caption">Launch and manage automatic software update packages.</p>

        <div style={{ background: 'var(--surface)', padding: 32, borderRadius: 16, marginTop: 40, boxShadow: '0 10px 25px -5px rgba(0,0,0,0.1)', border: '1px solid var(--outline)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
            <h2 style={{ color: 'var(--on-surface)', margin: 0 }}>Launch New App Update</h2>
            <button className="btn-small" style={{ background: 'transparent', border: '1px solid var(--outline)', color: 'var(--on-surface)' }} onClick={fetchGithubReleases} disabled={isFetchingGithub}>
              {isFetchingGithub ? 'Fetching...' : 'Fetch GitHub Releases'}
            </button>
          </div>

          {githubReleases.length > 0 && (
            <div style={{ marginBottom: 24, padding: 16, background: 'var(--surface-container-low)', borderRadius: 8, border: '1px solid var(--outline)' }}>
              <h3 style={{ fontSize: 14, margin: '0 0 12px 0', color: 'var(--text-muted)' }}>Latest GitHub Releases</h3>
              <div style={{ display: 'flex', gap: 12, overflowX: 'auto', paddingBottom: 8 }}>
                {githubReleases.slice(0, 5).map((r: any) => (
                  <div key={r.id} style={{ minWidth: 200, background: 'var(--surface)', padding: 12, borderRadius: 6, border: '1px solid var(--outline)' }}>
                    <div style={{ fontWeight: 'bold', fontSize: 14, marginBottom: 4 }}>{r.tag_name}</div>
                    <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{r.name}</div>
                    <button className="btn-small" style={{ width: '100%', fontSize: 12 }} onClick={() => autofillFromGithub(r)}>Autofill Form</button>
                  </div>
                ))}
              </div>
            </div>
          )}
          
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
            <div>
              <label className="caption">Target Version Code</label>
              <input type="text" className="input-field" placeholder="e.g. 1.1.0" value={version} onChange={e => setVersion(e.target.value)} style={{ padding: 12 }} />
            </div>
            
            <div>
              <label className="caption">Update Classification</label>
              <select className="input-field" value={updateType} onChange={e => setUpdateType(e.target.value as 'minor' | 'major')} style={{ padding: 12, height: 42, background: 'var(--background)', color: 'var(--on-surface)', border: '1px solid var(--outline)', borderRadius: 6, width: '100%' }}>
                <option value="minor">Minor Update (Free Bugfix / Refinement)</option>
                <option value="major">Major Update (Premium Version Upgrade)</option>
              </select>
            </div>

            <div style={{ gridColumn: '1 / -1' }}>
              <label className="caption">Binary / Update Package Download URL</label>
              <input type="text" className="input-field" placeholder="e.g. https://my-server.com/downloads/job_order_v1.1.0.zip" value={fileUrl} onChange={e => setFileUrl(e.target.value)} style={{ padding: 12 }} />
            </div>

            {updateType === 'major' && (
              <div>
                <label className="caption">Premium Upgrade Payment Amount ($ USD)</label>
                <div style={{ position: 'relative' }}>
                  <DollarSign size={16} style={{ position: 'absolute', left: 12, top: 13, color: 'var(--text-muted)' }} />
                  <input type="number" step="0.01" className="input-field" placeholder="19.99" value={paymentAmount} onChange={e => setPaymentAmount(e.target.value)} style={{ padding: '12px 12px 12px 32px' }} />
                </div>
              </div>
            )}

            <div style={{ gridColumn: '1 / -1' }}>
              <label className="caption">Update Changelog &amp; Feature Highlights (One per line)</label>
              <textarea className="input-field" placeholder="e.g.&#10;• Added Canva import auto-resizer.&#10;• Visual theme text-colors adjusted.&#10;• General database query optimizations." value={changelog} onChange={e => setChangelog(e.target.value)} style={{ padding: 12, height: 100, resize: 'vertical' }} />
            </div>

            <div style={{ gridColumn: '1 / -1', display: 'flex', flexDirection: 'column', gap: 12, marginTop: 4 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <input type="checkbox" id="compulsory" checked={isCompulsory} onChange={e => setIsCompulsory(e.target.checked)} style={{ width: 18, height: 18 }} />
                <label htmlFor="compulsory" style={{ color: 'var(--on-surface)', cursor: 'pointer', fontWeight: 500, display: 'flex', alignItems: 'center', gap: 6 }}>
                  {isCompulsory ? <ShieldAlert size={16} style={{ color: '#ef4444' }} /> : <Shield size={16} />}
                  Compulsory / Forceful Update (Skip button hidden, user must apply to continue)
                </label>
              </div>
            </div>
          </div>

          <button className="btn btn-primary" style={{ marginTop: 24, padding: '12px 24px', fontSize: 16 }} onClick={publishUpdate} disabled={isPublishing}>
            {isPublishing ? 'Publishing...' : <><Plus size={20} /> Publish to Update Server</>}
          </button>
        </div>

        <h2 style={{ marginTop: 60, marginBottom: 24, color: 'var(--on-surface)' }}>Published Releases</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {updates.map(u => (
            <div key={u.id} style={{ background: 'var(--surface)', padding: 24, borderRadius: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center', border: '1px solid var(--outline)', borderLeft: u.update_type === 'major' ? '6px solid #3b82f6' : '6px solid #64748b', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <div style={{ fontSize: 22, fontWeight: 'bold', color: 'var(--on-surface)' }}>v{u.version}</div>
                  {u.update_type === 'major' ? (
                    <span style={{ background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6', padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 'bold' }}>MAJOR UPGRADE (${u.payment_amount?.toFixed(2)})</span>
                  ) : (
                    <span style={{ background: 'rgba(100, 116, 139, 0.1)', color: 'var(--text-muted)', padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 'bold' }}>MINOR UPDATE</span>
                  )}
                  {u.is_compulsory && (
                    <span style={{ background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: 4 }}>
                      <ShieldAlert size={12} /> FORCE
                    </span>
                  )}
                </div>

                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 24, marginTop: 12, color: 'var(--text-muted)', fontSize: 14 }}>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6, maxWidth: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}><FileCode size={16} /> Package: {u.file_url}</span>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}><CheckCircle2 size={16} /> Released: {new Date(u.created_at).toLocaleDateString()}</span>
                </div>

                {u.changelog && (
                  <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px dashed var(--outline)' }}>
                    <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-muted)', marginBottom: 8, textTransform: 'uppercase' }}>Changelog Highlights</div>
                    <pre style={{ margin: 0, padding: 0, fontFamily: 'inherit', fontSize: 13, color: 'var(--on-surface)', whiteSpace: 'pre-wrap', lineHeight: '1.5' }}>{u.changelog}</pre>
                  </div>
                )}
              </div>

              <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                <button className="btn-small" style={{ color: '#ef4444', background: 'transparent', border: '1px solid #ef4444', padding: '8px 16px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }} onClick={() => deleteUpdate(u.id, u.version)} onMouseEnter={e => e.currentTarget.style.background = '#fee2e2'} onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <Trash2 size={16} /> Revoke Release
                </button>
              </div>
            </div>
          ))}
          {updates.length === 0 && <p className="caption" style={{ fontSize: 14 }}>No software updates published. Configure and publish a release above.</p>}
        </div>
      </div>
    </div>
  );
}
