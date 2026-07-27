import { useState, useEffect } from 'react';
import { supabase } from './supabase';
import { Key, Plus, Trash2, Shield, User, HardDrive, Edit, Cloud } from 'lucide-react';

export default function KeyManager({ isDarkMode: _isDarkMode }: { isDarkMode: boolean }) {
  const [keys, setKeys] = useState<any[]>([]);
  const [subscriptions, setSubscriptions] = useState<any[]>([]);
  const [email, setEmail] = useState('');
  const [selectedSubId, setSelectedSubId] = useState('');
  const [customMaxDevices, setCustomMaxDevices] = useState('');
  const [isFreeTrial, setIsFreeTrial] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);

  // Cloud sync options
  const [cloudSyncEnabled, setCloudSyncEnabled] = useState(false);
  const [cloudStorageLimit, setCloudStorageLimit] = useState('50'); // default to 50 MB
  const [cloudStorageUnit, setCloudStorageUnit] = useState('MB');

  // Edit license state
  const [editingKey, setEditingKey] = useState<any | null>(null);
  const [editSubId, setEditSubId] = useState('');
  const [editExpiresAt, setEditExpiresAt] = useState('');
  const [editCustomMax, setEditCustomMax] = useState('');
  const [editCloudSyncEnabled, setEditCloudSyncEnabled] = useState(false);
  const [editCloudStorageLimit, setEditCloudStorageLimit] = useState('50');
  const [editCloudStorageUnit, setEditCloudStorageUnit] = useState('MB');
  const [editCloudStorageUsed, setEditCloudStorageUsed] = useState('0');

  // Global settings for allocation limits
  const [isDigitalOceanEnabled, setIsDigitalOceanEnabled] = useState(false);
  const [remainingFreeSpaceMb, setRemainingFreeSpaceMb] = useState(500);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const { data: subs, error: subsError } = await supabase.from('subscriptions').select('*').order('created_at');
      if (subsError) {
        console.error("Error fetching subscriptions:", subsError);
      } else if (subs) {
        setSubscriptions(subs);
        if (subs.length > 0) setSelectedSubId(subs[0].id);
      }

      const { data: activeKeys, error: keysError } = await supabase.from('activation_keys')
        .select('*, subscriptions(name, max_devices), devices(device_name, hardware_id)')
        .order('created_at', { ascending: false });
      
      if (keysError) {
        console.error("Error fetching activation keys:", keysError);
      } else if (activeKeys) {
        setKeys(activeKeys);
        const totalAllocatedGb = activeKeys
          .filter(k => k.cloud_sync_enabled)
          .reduce((sum: number, item: any) => sum + parseFloat(item.cloud_storage_limit_gb || '0'), 0);
        const remainingMb = 500 - (totalAllocatedGb * 1024);
        setRemainingFreeSpaceMb(remainingMb);
      }

      const { data: settingsData, error: settingsError } = await supabase
        .from('system_settings')
        .select('value')
        .eq('key', 'is_digitalocean_enabled')
        .single();
      if (!settingsError && settingsData) {
        setIsDigitalOceanEnabled(settingsData.value === 'true');
      } else if (settingsError) {
        console.error("Error fetching settings:", settingsError);
      }
    } catch (e) {
      console.error('Unexpected error fetching data:', e);
    }
  };

  const generateKey = async () => {
    if (!email || !selectedSubId) return alert('Email and Subscription Tier are required.');
    
    const inputVal = parseFloat(cloudStorageLimit) || 50;
    const limitGb = cloudSyncEnabled 
      ? (cloudStorageUnit === 'MB' ? (inputVal / 1024) : inputVal)
      : 0.05;

    if (cloudSyncEnabled) {
      if (!isDigitalOceanEnabled) {
        const totalOtherAllocatedGb = keys
          .filter(k => k.cloud_sync_enabled)
          .reduce((sum, k) => sum + parseFloat(k.cloud_storage_limit_gb || '0'), 0);
        const totalWithNewGb = totalOtherAllocatedGb + limitGb;
        if (totalWithNewGb > 0.5) {
          alert(`Cannot allocate storage: Insufficient free space in Supabase tier. Limit: 0.5 GB. Total Allocated: ${totalOtherAllocatedGb.toFixed(3)} GB. Attempted: ${limitGb.toFixed(3)} GB (${(limitGb * 1024).toFixed(0)} MB). Enable DigitalOcean PocketBase first or allocate less space.`);
          return;
        }
      }
    }

    setIsGenerating(true);
    // Generate a random key like ABCD-1234-EFGH-5678
    const segments = Array.from({ length: 4 }, () => Math.random().toString(36).substring(2, 6).toUpperCase());
    const keyCode = segments.join('-');
    const staffKey = cloudSyncEnabled ? ("STF-" + Math.floor(100000 + Math.random() * 900000)) : null;

    const payload: any = {
      key_code: keyCode,
      staff_key: staffKey,
      email,
      subscription_id: selectedSubId,
      cloud_sync_enabled: cloudSyncEnabled,
      cloud_storage_limit_gb: limitGb,
      cloud_storage_used_mb: 0
    };

    if (customMaxDevices) {
      payload.custom_max_devices = parseInt(customMaxDevices);
    }

    if (isFreeTrial) {
      const expirationDate = new Date();
      expirationDate.setDate(expirationDate.getDate() + 7);
      payload.expires_at = expirationDate.toISOString();
      payload.is_trial = true;
    }

    const { error } = await supabase.from('activation_keys').insert([payload]);
    
    if (error) {
      alert('Error generating key: ' + error.message);
    } else {
      setEmail('');
      setCustomMaxDevices('');
      setCloudSyncEnabled(false);
      setCloudStorageLimit('50');
      setCloudStorageUnit('MB');
      fetchData();
    }
    setIsGenerating(false);
  };

  const saveKeyEdits = async () => {
    if (!editingKey) return;
    
    const inputVal = parseFloat(editCloudStorageLimit) || 50;
    const limitGb = editCloudSyncEnabled
      ? (editCloudStorageUnit === 'MB' ? (inputVal / 1024) : inputVal)
      : 0.05;

    if (editCloudSyncEnabled) {
      if (!isDigitalOceanEnabled) {
        const totalOtherAllocatedGb = keys
          .filter(k => k.id !== editingKey.id && k.cloud_sync_enabled)
          .reduce((sum, k) => sum + parseFloat(k.cloud_storage_limit_gb || '0'), 0);
        const totalWithNewGb = totalOtherAllocatedGb + limitGb;
        if (totalWithNewGb > 0.5) {
          alert(`Cannot allocate storage: Insufficient free space in Supabase tier. Limit: 0.5 GB. Total Allocated: ${totalOtherAllocatedGb.toFixed(3)} GB. Attempted: ${limitGb.toFixed(3)} GB (${(limitGb * 1024).toFixed(0)} MB). Enable DigitalOcean PocketBase first or allocate less space.`);
          return;
        }
      }
    }

    let updatedStaffKey = editingKey.staff_key;
    if (!editCloudSyncEnabled) {
      updatedStaffKey = null;
    } else if (!updatedStaffKey) {
      updatedStaffKey = "STF-" + Math.floor(100000 + Math.random() * 900000);
    }

    const payload: any = {
      subscription_id: editSubId,
      custom_max_devices: editCustomMax ? parseInt(editCustomMax) : null,
      expires_at: editExpiresAt ? new Date(editExpiresAt).toISOString() : null,
      cloud_sync_enabled: editCloudSyncEnabled,
      staff_key: updatedStaffKey,
      cloud_storage_limit_gb: limitGb,
      cloud_storage_used_mb: parseFloat(editCloudStorageUsed) || 0.0
    };

    const { error } = await supabase.from('activation_keys')
      .update(payload)
      .eq('id', editingKey.id);

    if (error) {
      alert('Error updating key: ' + error.message);
    } else {
      setEditingKey(null);
      fetchData();
    }
  };

  const deleteKey = async (id: string) => {
    if (!window.confirm("Revoke this key? This will disconnect all active devices.")) return;
    const { error } = await supabase.from('activation_keys').delete().eq('id', id);
    if (!error) fetchData();
  };

  const selectedTier = subscriptions.find(s => s.id === selectedSubId);
  const showCustomLimit = selectedTier?.max_devices === -1;

  return (
    <div className="dashboard-area" style={{ width: '100%', overflowY: 'auto', padding: '60px 40px' }}>
      <div style={{ maxWidth: 1200, margin: '0 auto' }}>
        <h1 className="headline-lg" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Key /> Activation Key Manager
        </h1>
        <p className="caption">Generate and manage licensing for your Service Memo application.</p>

        <div style={{ background: 'var(--surface)', padding: 32, borderRadius: 16, marginTop: 40, boxShadow: '0 10px 25px -5px rgba(0,0,0,0.1)', border: '1px solid var(--outline)' }}>
          <h2 style={{ marginBottom: 24, color: 'var(--on-surface)' }}>Generate New License</h2>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
            <div>
              <label className="caption">Customer Email</label>
              <input type="email" className="input-field" placeholder="client@company.com" value={email} onChange={e => setEmail(e.target.value)} style={{ padding: 12 }} />
            </div>
            <div>
              <label className="caption">Subscription Tier</label>
              <select className="input-field" value={selectedSubId} onChange={e => setSelectedSubId(e.target.value)} style={{ padding: 12 }}>
                {subscriptions.map(sub => (
                  <option key={sub.id} value={sub.id}>{sub.name} (Max Devices: {sub.max_devices === -1 ? 'Custom/Unlimited' : sub.max_devices})</option>
                ))}
              </select>
            </div>
            {showCustomLimit && (
              <div style={{ gridColumn: '1 / -1' }}>
                <label className="caption">Custom Device Limit (Optional)</label>
                <input type="number" className="input-field" placeholder="e.g. 50 (Leave blank for unlimited)" value={customMaxDevices} onChange={e => setCustomMaxDevices(e.target.value)} style={{ padding: 12 }} />
              </div>
            )}
            <div style={{ gridColumn: '1 / -1', display: 'flex', flexDirection: 'column', gap: 12, marginTop: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <input type="checkbox" id="freeTrial" checked={isFreeTrial} onChange={e => setIsFreeTrial(e.target.checked)} style={{ width: 18, height: 18 }} />
                <label htmlFor="freeTrial" style={{ color: 'var(--on-surface)', cursor: 'pointer', fontWeight: 500 }}>7-Day Free Trial (Key expires 7 days from now)</label>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <input type="checkbox" id="cloudSync" checked={cloudSyncEnabled} onChange={e => setCloudSyncEnabled(e.target.checked)} style={{ width: 18, height: 18 }} />
                <label htmlFor="cloudSync" style={{ color: 'var(--on-surface)', cursor: 'pointer', fontWeight: 500 }}>Enable Cloud Sync &amp; Storage Subscription</label>
              </div>
              {cloudSyncEnabled && (
                <div style={{ marginLeft: 28, display: 'flex', flexDirection: 'column', gap: 8 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <label className="caption" style={{ margin: 0 }}>Storage Limit:</label>
                    <div style={{ display: 'flex', alignItems: 'center', border: '1px solid var(--outline)', borderRadius: 6, overflow: 'hidden', background: 'var(--background)' }}>
                      <input 
                        type="number" 
                        step="any" 
                        min="0.01" 
                        value={cloudStorageLimit} 
                        onChange={e => setCloudStorageLimit(e.target.value)} 
                        style={{ padding: '8px 12px', width: 100, border: 'none', background: 'transparent', color: 'var(--on-surface)', outline: 'none' }} 
                      />
                      <select 
                        value={cloudStorageUnit} 
                        onChange={e => setCloudStorageUnit(e.target.value)} 
                        style={{ padding: '8px 8px', border: 'none', borderLeft: '1px solid var(--outline)', background: 'var(--background)', color: 'var(--on-surface)', outline: 'none', fontWeight: 'bold', cursor: 'pointer' }}
                      >
                        <option value="MB">MB</option>
                        <option value="GB">GB</option>
                      </select>
                    </div>
                    <span style={{ fontSize: 12, fontWeight: 'bold', color: 'var(--primary)' }}>
                      ({cloudStorageUnit === 'MB' 
                        ? `${(parseFloat(cloudStorageLimit) || 0).toFixed(0)} MB` 
                        : `${((parseFloat(cloudStorageLimit) || 0) * 1024).toFixed(0)} MB`
                      })
                    </span>
                  </div>
                  <small style={{ color: 'var(--text-muted)', fontSize: 12 }}>
                    {isDigitalOceanEnabled 
                      ? '✓ DigitalOcean PocketBase is enabled (Unlimited SSD storage).'
                      : `Supabase Free Pool Capacity remaining: ${remainingFreeSpaceMb.toFixed(1)} MB.`
                    }
                  </small>
                </div>
              )}
            </div>
          </div>
          <button className="btn btn-primary" style={{ marginTop: 24, padding: '12px 24px', fontSize: 16 }} onClick={generateKey} disabled={isGenerating}>
            {isGenerating ? 'Generating...' : <><Plus size={20} /> Generate Key</>}
          </button>
        </div>

        <h2 style={{ marginTop: 60, marginBottom: 24, color: 'var(--on-surface)' }}>Active Licenses</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {keys.map(k => (
            <div key={k.id} style={{ background: 'var(--surface)', padding: 24, borderRadius: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center', border: '1px solid var(--outline)', borderLeft: k.is_active ? '6px solid #10b981' : '6px solid #ef4444', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)' }}>
              <div>
                <div style={{ fontSize: 20, fontFamily: 'monospace', fontWeight: 'bold', color: 'var(--primary)' }}>{k.key_code}</div>
                <div style={{ fontSize: 13, fontFamily: 'monospace', color: 'var(--text-muted)', marginTop: 4 }}>
                  Staff Login Key: <span style={{ fontWeight: 'bold', color: k.staff_key ? '#3b82f6' : '#ef4444' }}>{k.staff_key || 'Disabled (Cloud Sync Off)'}</span>
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 24, marginTop: 12, color: 'var(--text-muted)', fontSize: 14 }}>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}><User size={16} /> {k.email}</span>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}><Shield size={16} /> {k.subscriptions?.name} Tier{k.is_trial && <span style={{ marginLeft: 8, background: '#f59e0b', color: '#fff', padding: '2px 6px', borderRadius: 4, fontSize: 10, fontWeight: 'bold' }}>TRIAL</span>}</span>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}><HardDrive size={16} /> Limit: {k.custom_max_devices || (k.subscriptions?.max_devices === -1 ? 'Unlimited' : k.subscriptions?.max_devices)} devices</span>
                  {k.expires_at && (
                    <span style={{ display: 'flex', alignItems: 'center', gap: 6, color: new Date(k.expires_at) < new Date() ? '#ef4444' : '#f59e0b' }}>
                       Expires: {new Date(k.expires_at).toLocaleDateString()}
                    </span>
                  )}
                  <span style={{ display: 'flex', flexDirection: 'column', gap: 4, color: k.cloud_sync_enabled ? '#10b981' : 'var(--text-muted)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Cloud size={16} />
                      <span>
                        Cloud Sync: {k.cloud_sync_enabled 
                          ? `Enabled (${parseFloat(k.cloud_storage_limit_gb || '0') < 1.0
                              ? `${(k.cloud_storage_used_mb || 0).toFixed(2)} MB / ${(parseFloat(k.cloud_storage_limit_gb) * 1024).toFixed(0)} MB`
                              : `${((k.cloud_storage_used_mb || 0) / 1024).toFixed(2)} GB / ${parseFloat(k.cloud_storage_limit_gb).toFixed(1)} GB`
                            })`
                          : 'Disabled'
                        }
                      </span>
                    </div>
                    {k.cloud_sync_enabled && parseFloat(k.cloud_storage_limit_gb || '0') >= 1.0 && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.75rem', color: 'var(--text-muted)', opacity: 0.8, marginLeft: 22 }}>
                        <HardDrive size={12} />
                        <span>({(k.cloud_storage_used_mb || 0).toFixed(2)} MB / ${(parseFloat(k.cloud_storage_limit_gb) * 1024).toFixed(0)} MB)</span>
                      </div>
                    )}
                  </span>
                </div>
                
                {k.devices && k.devices.length > 0 && (
                  <div style={{ marginTop: 16, paddingTop: 16, borderTop: '1px dashed var(--outline)' }}>
                    <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-muted)', marginBottom: 8, textTransform: 'uppercase' }}>Registered Devices ({k.devices.length})</div>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                      {k.devices.map((d: any, idx: number) => (
                        <div key={idx} style={{ background: 'var(--background)', border: '1px solid var(--outline)', padding: '4px 10px', borderRadius: 20, fontSize: 12, color: 'var(--on-surface)', display: 'flex', alignItems: 'center', gap: 6 }}>
                          <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#10b981' }}></span>
                          {d.device_name || 'Unknown Device'} <span style={{ opacity: 0.5 }}>({d.hardware_id?.substring(0, 8)}...)</span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
               <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                <button className="btn-small" style={{ color: 'var(--primary)', background: 'transparent', border: '1px solid var(--primary)', padding: '8px 16px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }} onClick={() => {
                  setEditingKey(k);
                  setEditSubId(k.subscription_id);
                  setEditExpiresAt(k.expires_at ? k.expires_at.substring(0, 10) : '');
                  setEditCustomMax(k.custom_max_devices || '');
                  setEditCloudSyncEnabled(k.cloud_sync_enabled || false);
                  
                  const limitGb = parseFloat(k.cloud_storage_limit_gb || '0.05');
                  if (limitGb < 1.0) {
                    setEditCloudStorageLimit(parseFloat((limitGb * 1024).toFixed(2)).toString());
                    setEditCloudStorageUnit('MB');
                  } else {
                    setEditCloudStorageLimit(limitGb.toString());
                    setEditCloudStorageUnit('GB');
                  }
                  
                  setEditCloudStorageUsed(k.cloud_storage_used_mb || '0');
                }}>
                  <Edit size={16} /> Edit
                </button>
                <button className="btn-small" style={{ color: '#ef4444', background: 'transparent', border: '1px solid #ef4444', padding: '8px 16px', fontSize: 14, display: 'flex', alignItems: 'center', gap: 6 }} onClick={() => deleteKey(k.id)} onMouseEnter={e => e.currentTarget.style.background = '#fee2e2'} onMouseLeave={e => e.currentTarget.style.background = 'transparent'}>
                  <Trash2 size={16} /> Revoke
                </button>
              </div>
            </div>
          ))}
          {keys.length === 0 && <p className="caption" style={{ fontSize: 14 }}>No active keys found. Generate one above to get started.</p>}
        </div>
      </div>

      {editingKey && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.6)', backdropFilter: 'blur(4px)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000
        }}>
          <div style={{
            background: 'var(--surface)', padding: 32, borderRadius: 16,
            width: '90%', maxWidth: 500, border: '1px solid var(--outline)',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.3)'
          }}>
            <h2 style={{ marginBottom: 24, color: 'var(--on-surface)', display: 'flex', alignItems: 'center', gap: 10 }}><Edit /> Edit Plan &amp; Validity</h2>
            <p className="caption" style={{ marginBottom: 20 }}>License Key: <strong style={{ color: 'var(--primary)' }}>{editingKey.key_code}</strong></p>
            
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              <div>
                <label className="caption">Subscription Tier</label>
                <select className="input-field" value={editSubId} onChange={e => setEditSubId(e.target.value)} style={{ padding: 12, width: '100%', border: '1px solid var(--outline)', borderRadius: 8, background: 'var(--background)', color: 'var(--on-surface)' }}>
                  {subscriptions.map(sub => (
                    <option key={sub.id} value={sub.id}>{sub.name} (Max Devices: {sub.max_devices === -1 ? 'Custom/Unlimited' : sub.max_devices})</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="caption">Expiration Date</label>
                <input 
                  type="date" 
                  className="input-field" 
                  value={editExpiresAt} 
                  onChange={e => setEditExpiresAt(e.target.value)} 
                  style={{ padding: 12, width: '100%', border: '1px solid var(--outline)', borderRadius: 8, background: 'var(--background)', color: 'var(--on-surface)' }}
                />
                <small style={{ color: 'var(--text-muted)', display: 'block', marginTop: 4 }}>Leave blank for no expiration (unlimited duration)</small>
              </div>

              {subscriptions.find(s => s.id === editSubId)?.max_devices === -1 && (
                <div>
                  <label className="caption">Custom Device Limit (Optional)</label>
                  <input 
                    type="number" 
                    className="input-field" 
                    placeholder="e.g. 50 (Leave blank for unlimited)" 
                    value={editCustomMax} 
                    onChange={e => setEditCustomMax(e.target.value)} 
                    style={{ padding: 12, width: '100%', border: '1px solid var(--outline)', borderRadius: 8, background: 'var(--background)', color: 'var(--on-surface)' }}
                  />
                </div>
              )}

              <div style={{ display: 'flex', flexDirection: 'column', gap: 12, borderTop: '1px dashed var(--outline)', paddingTop: 16, marginTop: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <input type="checkbox" id="editCloudSync" checked={editCloudSyncEnabled} onChange={e => setEditCloudSyncEnabled(e.target.checked)} style={{ width: 18, height: 18 }} />
                  <label htmlFor="editCloudSync" style={{ color: 'var(--on-surface)', cursor: 'pointer', fontWeight: 500 }}>Cloud Sync Enabled</label>
                </div>
                {editCloudSyncEnabled && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                      <div>
                        <label className="caption">Storage Limit</label>
                        <div style={{ display: 'flex', alignItems: 'center', border: '1px solid var(--outline)', borderRadius: 8, overflow: 'hidden', background: 'var(--background)', width: '100%' }}>
                          <input 
                            type="number" 
                            step="any" 
                            min="0.01" 
                            value={editCloudStorageLimit} 
                            onChange={e => setEditCloudStorageLimit(e.target.value)} 
                            style={{ padding: 10, flex: 1, border: 'none', background: 'transparent', color: 'var(--on-surface)', outline: 'none' }} 
                          />
                          <select 
                            value={editCloudStorageUnit} 
                            onChange={e => setEditCloudStorageUnit(e.target.value)} 
                            style={{ padding: 10, border: 'none', borderLeft: '1px solid var(--outline)', background: 'var(--background)', color: 'var(--on-surface)', outline: 'none', fontWeight: 'bold', cursor: 'pointer' }}
                          >
                            <option value="MB">MB</option>
                            <option value="GB">GB</option>
                          </select>
                        </div>
                        <small style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--primary)', display: 'block', marginTop: 4 }}>
                          ({editCloudStorageUnit === 'MB' 
                            ? `${(parseFloat(editCloudStorageLimit) || 0).toFixed(0)} MB` 
                            : `${((parseFloat(editCloudStorageLimit) || 0) * 1024).toFixed(0)} MB`
                          })
                        </small>
                      </div>
                      <div>
                        <label className="caption">Storage Used (MB)</label>
                        <input type="number" step="0.01" className="input-field" value={editCloudStorageUsed} onChange={e => setEditCloudStorageUsed(e.target.value)} style={{ padding: 10, width: '100%', border: '1px solid var(--outline)', borderRadius: 8, background: 'var(--background)', color: 'var(--on-surface)' }} />
                      </div>
                    </div>
                    <small style={{ color: 'var(--text-muted)', fontSize: 12 }}>
                      {isDigitalOceanEnabled 
                        ? '✓ DigitalOcean PocketBase is enabled.'
                        : `Supabase Free Pool Capacity remaining (excluding this key): ${(remainingFreeSpaceMb + (parseFloat(editingKey.cloud_storage_limit_gb || '0') * 1024)).toFixed(1)} MB.`
                      }
                    </small>
                  </div>
                )}
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 32 }}>
              <button 
                className="btn" 
                style={{ padding: '10px 20px', background: 'transparent', border: '1px solid var(--outline)', color: 'var(--on-surface)', cursor: 'pointer', borderRadius: 8 }} 
                onClick={() => setEditingKey(null)}
              >
                Cancel
              </button>
              <button 
                className="btn btn-primary" 
                style={{ padding: '10px 20px', cursor: 'pointer', borderRadius: 8 }} 
                onClick={saveKeyEdits}
              >
                Save Changes
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
