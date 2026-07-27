import React, { useState, useEffect } from 'react'
import { 
  Monitor, 
  Shield, 
  Cloud, 
  Play, 
  Check, 
  Copy, 
  LogOut, 
  ChevronRight, 
  Lock, 
  Key,
  Mail, 
  User, 
  Building, 
  Sun, 
  Moon, 
  Download, 
  Laptop, 
  Layers, 
  Zap, 
  Sparkles,
  CheckCircle,
  Phone,
  CreditCard,
  LockKeyhole,
  Activity,
  AlertTriangle,
  CloudOff,
  HardDrive,
  Settings
} from 'lucide-react'
import { supabase } from './supabase'
import './App.css'

function App() {
  const renderStorageText = (usedMb: number, limitGb: number) => {
    const isUnder1Gb = limitGb < 1.0;
    const limitMb = limitGb * 1024;
    
    if (isUnder1Gb) {
      return (
        <span style={{ fontWeight: 800 }}>
          {usedMb.toFixed(2)} MB / {limitMb.toFixed(0)} MB
        </span>
      );
    } else {
      const usedGb = usedMb / 1024;
      return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
          <span style={{ fontWeight: 800 }}>
            {usedGb.toFixed(2)} GB / {limitGb.toFixed(1)} GB
          </span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.75rem', color: 'var(--on-surface-variant)', opacity: 0.8, marginTop: 2 }}>
            <HardDrive size={12} style={{ color: 'var(--primary)' }} />
            <span>({usedMb.toFixed(2)} MB / {limitMb.toFixed(0)} MB)</span>
          </div>
        </div>
      );
    }
  };

  const [activeTab, setActiveTab] = useState<'home' | 'products' | 'company' | 'dashboard'>('home')
  const [selectedProduct, setSelectedProduct] = useState<'generator' | 'billing'>('generator')
  const [copiedKey, setCopiedKey] = useState<string | null>(null)
  const [productView, setProductView] = useState<'list' | 'details'>('list')
  const [showPricingBelowApp, setShowPricingBelowApp] = useState(false)
  const [isDarkMode, setIsDarkMode] = useState(() => {
    return localStorage.getItem('memobud_theme') === 'dark'
  })
  
  // Auth Form State
  const [showAuthModal, setShowAuthModal] = useState(false)
  const [authMode, setAuthMode] = useState<'login' | 'signup' | 'forgot' | 'otp_verify' | 'reset_password' | 'unconfirmed_email'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fullName, setFullName] = useState('')
  const [companyName, setCompanyName] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [countryCode, setCountryCode] = useState('+1')
  
  // OTP Reset Flow States
  const [otpCode, setOtpCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')

  // Rate Limit Tracking States
  const [forgotAttempts, setForgotAttempts] = useState<number>(0)
  const [loginAttempts, setLoginAttempts] = useState<number>(0)
  const [forgotLockoutTime, setForgotLockoutTime] = useState<number>(0)
  const [loginLockoutTime, setLoginLockoutTime] = useState<number>(0)

  // Per-Email rate limit lookup helper functions
  const getLoginAttemptsForEmail = (emailStr: string): number => {
    if (!emailStr) return 0
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_login_attempts_map') || '{}')
      return map[cleanEmail] || 0
    } catch {
      return 0
    }
  }

  const setLoginAttemptsForEmail = (emailStr: string, attempts: number) => {
    if (!emailStr) return
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_login_attempts_map') || '{}')
      map[cleanEmail] = attempts
      localStorage.setItem('mb_login_attempts_map', JSON.stringify(map))
    } catch {}
  }

  const getLoginLockoutForEmail = (emailStr: string): number => {
    if (!emailStr) return 0
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_login_lockouts_map') || '{}')
      return map[cleanEmail] || 0
    } catch {
      return 0
    }
  }

  const setLoginLockoutForEmail = (emailStr: string, timestamp: number) => {
    if (!emailStr) return
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_login_lockouts_map') || '{}')
      map[cleanEmail] = timestamp
      localStorage.setItem('mb_login_lockouts_map', JSON.stringify(map))
    } catch {}
  }

  const getForgotAttemptsForEmail = (emailStr: string): number => {
    if (!emailStr) return 0
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_forgot_attempts_map') || '{}')
      return map[cleanEmail] || 0
    } catch {
      return 0
    }
  }

  const setForgotAttemptsForEmail = (emailStr: string, attempts: number) => {
    if (!emailStr) return
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_forgot_attempts_map') || '{}')
      map[cleanEmail] = attempts
      localStorage.setItem('mb_forgot_attempts_map', JSON.stringify(map))
    } catch {}
  }

  const getForgotLockoutForEmail = (emailStr: string): number => {
    if (!emailStr) return 0
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_forgot_lockouts_map') || '{}')
      return map[cleanEmail] || 0
    } catch {
      return 0
    }
  }

  const setForgotLockoutForEmail = (emailStr: string, timestamp: number) => {
    if (!emailStr) return
    const cleanEmail = emailStr.trim().toLowerCase()
    try {
      const map = JSON.parse(localStorage.getItem('mb_forgot_lockouts_map') || '{}')
      map[cleanEmail] = timestamp
      localStorage.setItem('mb_forgot_lockouts_map', JSON.stringify(map))
    } catch {}
  }

  // Synchronize state when email changes
  useEffect(() => {
    setForgotAttempts(getForgotAttemptsForEmail(email))
    setLoginAttempts(getLoginAttemptsForEmail(email))
    setForgotLockoutTime(getForgotLockoutForEmail(email))
    setLoginLockoutTime(getLoginLockoutForEmail(email))
  }, [email])

  // New Trial Enrollment Modal State
  const [showTrialFormModal, setShowTrialFormModal] = useState(false)
  const [trialName, setTrialName] = useState('')
  const [trialEmail, setTrialEmail] = useState('')
  const [trialPhone, setTrialPhone] = useState('')
  
  // Pending Transaction State (holds what user wants to buy/register next)
  const [pendingAction, setPendingAction] = useState<{
    type: 'license' | 'cloud';
    tier: any;
  } | null>(null)
  
  // Payment Gateway Modal State
  const [showPaymentModal, setShowPaymentModal] = useState(false)
  const [checkoutStep, setCheckoutStep] = useState<'contact' | 'razorpay'>('contact')
  const [billingName, setBillingName] = useState('')
  const [billingEmail, setBillingEmail] = useState('')
  const [billingPhone, setBillingPhone] = useState('')
  const [razorpayMethod, setRazorpayMethod] = useState<'upi' | 'card' | 'netbanking' | 'qr' | null>(null)
  
  const [cardName, setCardName] = useState('')
  const [cardNumber, setCardNumber] = useState('')
  const [cardExpiry, setCardExpiry] = useState('')
  const [cardCvv, setCardCvv] = useState('')
  const [isProcessingPayment, setIsProcessingPayment] = useState(false)
  const [paymentSuccessData, setPaymentSuccessData] = useState<{
    keyCode: string;
    tierName: string;
    expiresAt: string;
    isTrial: boolean;
  } | null>(null)

  // Logged In User State
  const [currentUser, setCurrentUser] = useState<any | null>(null)
  const [, setDashboardTab] = useState<'overview' | 'licenses' | 'devices'>('overview')
  const [selectedDashboardProduct, setSelectedDashboardProduct] = useState<'generator' | 'billing' | 'staff' | 'settings'>('generator')
  const [userKeys, setUserKeys] = useState<any[]>([])
  const [selectedKeyForDevices, setSelectedKeyForDevices] = useState<string | null>(null)
  const [, setDevicesForKey] = useState<any[]>([])

  // Account Settings State
  const [oldPasswordSettings, setOldPasswordSettings] = useState('')
  const [newPasswordSettings, setNewPasswordSettings] = useState('')
  const [confirmNewPasswordSettings, setConfirmNewPasswordSettings] = useState('')
  const [isChangingPasswordSettings, setIsChangingPasswordSettings] = useState(false)
  const [selectedKeyToClear, setSelectedKeyToClear] = useState('')
  const [isClearingDataSettings, setIsClearingDataSettings] = useState(false)
  const [clearStep, setClearStep] = useState<0 | 1 | 2 | 3 | 4>(0)

  // Staff Management State
  const [staffKeyString, setStaffKeyString] = useState<string | null>(null)
  const [staffList, setStaffList] = useState<any[]>([])
  const [showAddStaffModal, setShowAddStaffModal] = useState(false)
  const [staffName, setStaffName] = useState('')
  const [staffEmail, setStaffEmail] = useState('')
  const [staffPhone, setStaffPhone] = useState('')
  const [staffPassword, setStaffPassword] = useState('')
  const [isSavingStaff, setIsSavingStaff] = useState(false)

  // Interactive Live Canvas Playground State
  const [selectedCell, setSelectedCell] = useState<string>('header')
  const [canvasStyles, setCanvasStyles] = useState({
    header: { bg: '#004f96', color: '#ffffff', bold: true },
    qty: { bg: '#ffffff', color: '#191c1e', bold: false },
    desc: { bg: '#ffffff', color: '#191c1e', bold: false },
    price: { bg: '#ffffff', color: '#191c1e', bold: false }
  })
  
  // Video Player state
  const [isVideoPlaying, setIsVideoPlaying] = useState(false)

  // Cloud Sync Upgrade Modal State
  const [showCloudUpgradeModal, setShowCloudUpgradeModal] = useState(false)
  const [selectedUpgradeKeyId, setSelectedUpgradeKeyId] = useState<string | null>(null)
  // const [customStorageAmount, setCustomStorageAmount] = useState<number | ''>(10)
  const [isProcessingUpgrade, _setIsProcessingUpgrade] = useState(false)
  const [pendingCloudUpgrade, setPendingCloudUpgrade] = useState<{
    keyId: string;
    gb: number;
    price: number;
    planName: string;
    devices: number;
  } | null>(null)

  // License Plan Upgrade Modal State
  const [showPlanUpgradeModal, setShowPlanUpgradeModal] = useState(false)
  const [selectedPlanUpgradeKeyId, setSelectedPlanUpgradeKeyId] = useState<string | null>(null)
  const [pendingPlanUpgrade, setPendingPlanUpgrade] = useState<{
    keyId: string;
    tier: 'basic' | 'pro' | 'enterprise' | 'unlimited';
    planName: string;
    price: number;
  } | null>(null)

  // Cloud Sync Deactivate Safeguard Modal State
  const [showCloudDeactivateModal, setShowCloudDeactivateModal] = useState(false)
  const [deactivateConfirmText, setDeactivateConfirmText] = useState('')
  const [selectedDeactivateKeyId, setSelectedDeactivateKeyId] = useState<string | null>(null)

  // Website Custom Pricing Calculators State
  const [pricingCategory, setPricingCategory] = useState<'with_cloud' | 'without_cloud'>('with_cloud')
  const [customCloudDevices, setCustomCloudDevices] = useState(5)
  const [customCloudStorage, setCustomCloudStorage] = useState(0.5) // default 500MB
  const [customLocalDevices, setCustomLocalDevices] = useState(2)

  const calculateCustomLocalPrice = (devices: number) => {
    if (devices === 1) return 15000;
    if (devices <= 3) return 15000 + (devices - 1) * 1000;
    if (devices <= 10) return 17000 + Math.round((devices - 3) * 857.14);
    return 23000 + (devices - 10) * 700;
  };

  const calculateCustomCloudPrice = (devices: number, gb: number) => {
    const baseStoragePrice = (() => {
      if (gb <= 0.05) return 300;
      if (gb <= 0.1) return 300 + ((gb - 0.05) / 0.05) * 200;
      if (gb <= 0.5) return 500 + ((gb - 0.1) / 0.4) * 1000;
      if (gb <= 1.0) return 1500 + ((gb - 0.5) / 0.5) * 1000;
      if (gb <= 3.0) return 2500 + ((gb - 1.0) / 2.0) * 2500;
      return 5000 + (gb - 3.0) * 1000;
    })();
    
    const defaultDevices = (() => {
      if (gb < 0.1) return 3;
      if (gb < 0.5) return 5;
      if (gb < 1.0) return 10;
      return 15;
    })();
    
    const extraDevices = Math.max(0, devices - defaultDevices);
    return Math.round(baseStoragePrice + extraDevices * 100);
  };

  // Subscriptions UUID references
  const BASIC_SUB_ID = '2f96861a-0ce3-418f-a513-34883b2252da'
  const PRO_SUB_ID = '0ea0a705-63ef-49f8-b0db-aa6e29f4ddae'
  const ENTERPRISE_SUB_ID = 'c88c0f4c-a999-4904-abf4-aa587fed8679'

  const [isDigitalOceanEnabled, setIsDigitalOceanEnabled] = useState(false)
  const [remainingFreeSpaceMb, setRemainingFreeSpaceMb] = useState(500)

  // Admin settings states removed - moved to CloudAdmin app.

  // Effect to toggle dark mode class on document element
  useEffect(() => {
    const root = window.document.body
    if (isDarkMode) {
      root.classList.add('dark-theme')
      localStorage.setItem('memobud_theme', 'dark')
    } else {
      root.classList.remove('dark-theme')
      localStorage.setItem('memobud_theme', 'light')
    }
  }, [isDarkMode])

  // Fetch system settings
  const fetchSystemSettings = async () => {
    try {
      const { data, error } = await supabase
        .from('system_settings')
        .select('value')
        .eq('key', 'is_digitalocean_enabled')
        .single()
      if (!error && data) {
        setIsDigitalOceanEnabled(data.value === 'true')
      }
    } catch (e) {
      console.error('Error fetching system settings:', e)
    }
  }

  // Fetch remaining free storage space
  const fetchRemainingFreeSpace = async () => {
    try {
      const { data, error } = await supabase
        .from('activation_keys')
        .select('cloud_storage_limit_gb')
        .eq('cloud_sync_enabled', true)
      if (!error && data) {
        const totalAllocatedGb = data.reduce((sum: number, item: any) => sum + parseFloat(item.cloud_storage_limit_gb || '0'), 0)
        const remainingMb = 500 - (totalAllocatedGb * 1024)
        setRemainingFreeSpaceMb(remainingMb)
      }
    } catch (e) {
      console.error('Error calculating remaining free space:', e)
    }
  }

  // Admin key generation and DO helper functions removed - moved to CloudAdmin app.

  // Load user data and listen to auth changes
  useEffect(() => {
    fetchSystemSettings()
    fetchRemainingFreeSpace()

    // 1. Initial local storage check
    const savedUser = localStorage.getItem('memobud_user')
    if (savedUser) {
      const parsed = JSON.parse(savedUser)
      setCurrentUser(parsed)
      fetchUserLicenses(parsed.email)
    }

    // 2. Initial session check from Supabase
    supabase.auth.getSession().then(({ data: { session } }) => {
      if (session && session.user) {
        const metadata = session.user.user_metadata || {}
        const loggedUser = {
          email: session.user.email,
          name: metadata.full_name || session.user.email?.split('@')[0].toUpperCase(),
          company: metadata.company_name || 'Partner Enterprise Corp',
          phone: metadata.phone_number || 'Not Registered Yet',
          id: session.user.id
        }
        localStorage.setItem('memobud_user', JSON.stringify(loggedUser))
        setCurrentUser(loggedUser)
        fetchUserLicenses(session.user.email!)
      }
    })

    // 3. Listen for auth state changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange((event, session) => {
      if (event === 'SIGNED_IN' && session && session.user) {
        const metadata = session.user.user_metadata || {}
        const loggedUser = {
          email: session.user.email,
          name: metadata.full_name || session.user.email?.split('@')[0].toUpperCase(),
          company: metadata.company_name || 'Partner Enterprise Corp',
          phone: metadata.phone_number || 'Not Registered Yet',
          id: session.user.id
        }
        localStorage.setItem('memobud_user', JSON.stringify(loggedUser))
        setCurrentUser(loggedUser)
        fetchUserLicenses(session.user.email!)
      } else if (event === 'SIGNED_OUT') {
        localStorage.removeItem('memobud_user')
        setCurrentUser(null)
        setUserKeys([])
        setSelectedKeyForDevices(null)
        setDevicesForKey([])
      }
    })

    return () => {
      subscription.unsubscribe()
    }
  }, [])

  // Fetch licenses matching the user's email
  const fetchUserLicenses = async (userEmail: string) => {
    try {
      const { data: keys, error } = await supabase
        .from('activation_keys')
        .select('*, subscriptions(name, max_devices), devices(*)')
        .eq('email', userEmail)
        .order('created_at', { ascending: false })
      
      if (error) console.error(error)
      
      if (keys) {
        // Filter out ST- and SFK- prefixed keys from the primary license key list
        const filteredKeys = keys.filter(k => !k.key_code.startsWith('ST-') && !k.key_code.startsWith('SFK-'))
        setUserKeys(filteredKeys)

        if (filteredKeys.length > 0 && !selectedKeyForDevices) {
          setSelectedKeyForDevices(filteredKeys[0].id)
          setDevicesForKey(filteredKeys[0].devices || [])
          fetchStaffList(filteredKeys[0].id)
          fetchStaffKey(filteredKeys[0].id)
        } else if (selectedKeyForDevices) {
          const currentKeyObj = filteredKeys.find(k => k.id === selectedKeyForDevices)
          setDevicesForKey(currentKeyObj ? currentKeyObj.devices : [])
          if (currentKeyObj) {
            fetchStaffList(currentKeyObj.id)
            fetchStaffKey(currentKeyObj.id)
          }
        }
      }
    } catch (e) {
      console.error(e)
    }
  }

  // Fetch staff list from Supabase
  const fetchStaffList = async (keyId: string) => {
    try {
      const { data, error } = await supabase
        .from('staff')
        .select('*')
        .eq('activation_key_id', keyId)
        .order('created_at', { ascending: false })
      if (!error && data) {
        setStaffList(data.filter((s: any) => s.name !== '__STAFF_KEY__' && !s.email?.startsWith('SMK-')))
      }
    } catch (e) {
      console.error('Error fetching staff list:', e)
    }
  }

  // Fetch staff activation key directly from activation_keys table
  const fetchStaffKey = async (keyId: string) => {
    try {
      const { data, error } = await supabase
        .from('activation_keys')
        .select('staff_key, cloud_sync_enabled')
        .eq('id', keyId)
        .single()
      
      if (!error && data) {
        if (data.cloud_sync_enabled && data.staff_key) {
          setStaffKeyString(data.staff_key)
        } else {
          setStaffKeyString('Disabled (Cloud Sync Off)')
        }
      }
    } catch (e) {
      console.error('Error fetching staff key:', e)
    }
  }

  // Add staff member to Supabase
  const handleAddStaff = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!staffName || !staffEmail || !staffPhone || !staffPassword) {
      return alert('All fields are required to register a staff member.')
    }
    if (userKeys.length === 0) {
      return alert('You must have an active license key to manage staff.')
    }

    setIsSavingStaff(true)
    try {
      const { error } = await supabase
        .from('staff')
        .insert([{
          activation_key_id: userKeys[0].id,
          name: staffName,
          email: staffEmail,
          phone_number: staffPhone,
          password: staffPassword
        }])
        .select()

      if (error) {
        alert('Failed to add staff member: ' + error.message)
      } else {
        alert('Staff member registered successfully!')
        setShowAddStaffModal(false)
        setStaffName('')
        setStaffEmail('')
        setStaffPhone('')
        setStaffPassword('')
        fetchStaffList(userKeys[0].id)
      }
    } catch (err: any) {
      alert('Error saving staff: ' + err.message)
    } finally {
      setIsSavingStaff(false)
    }
  }

  // Remove staff member from Supabase
  const handleRemoveStaff = async (staffId: string) => {
    if (!window.confirm('Are you sure you want to remove this staff member? They will lose access to the mobile app instantly.')) return
    try {
      const { error } = await supabase
        .from('staff')
        .delete()
        .eq('id', staffId)

      if (error) {
        alert('Failed to remove staff: ' + error.message)
      } else {
        if (userKeys.length > 0) {
          fetchStaffList(userKeys[0].id)
        }
      }
    } catch (err: any) {
      alert('Error removing staff: ' + err.message)
    }
  }

  // Handle license copy to clipboard
  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
    setCopiedKey(text)
    setTimeout(() => setCopiedKey(null), 2000)
  }

  // Handle Forgot Password submission
  const handleForgotPassword = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email) return alert('Please enter your email address.')

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailRegex.test(email)) return alert('Please enter a valid email address.')

    // Check Lockout
    if (forgotLockoutTime && Date.now() < forgotLockoutTime) {
      const minutesLeft = Math.ceil((forgotLockoutTime - Date.now()) / 60000)
      return alert(`Forgot password requests are temporarily disabled. Please try again in ${minutesLeft} minutes.`)
    }

    try {
      // 1. Explicitly check if the email has an account/profile or activation key in our database
      const { data: keysData } = await supabase
        .from('activation_keys')
        .select('id')
        .eq('email', email)
        .limit(1)

      const { data: profilesData } = await supabase
        .from('company_profiles')
        .select('email_id')
        .eq('email_id', email)
        .limit(1)

      // If email doesn't exist anywhere, return error
      if ((!keysData || keysData.length === 0) && (!profilesData || profilesData.length === 0)) {
        return alert(`Reset Failed: The email "${email}" is not registered in our database. Please check the email spelling or register a new profile.`)
      }

      // Track Attempt
      let nextAttempts = forgotAttempts + 1
      if (forgotLockoutTime && Date.now() > forgotLockoutTime) {
        // If lockout expired, reset counter to 1
        nextAttempts = 1
        setForgotLockoutForEmail(email, 0)
        setForgotLockoutTime(0)
      }
      setForgotAttempts(nextAttempts)
      setForgotAttemptsForEmail(email, nextAttempts)

      if (nextAttempts >= 10) {
        const lockout = Date.now() + 15 * 60 * 1000
        setForgotLockoutForEmail(email, lockout)
        setForgotLockoutTime(lockout)
        alert(`You have reached the maximum of 10 forgot password requests. Reset requests are disabled for 15 minutes.`)
      }

      const { error } = await supabase.auth.resetPasswordForEmail(email, {
        redirectTo: window.location.origin
      })

      if (error) {
        return alert('Password Reset Failed: ' + error.message)
      }

      alert('A 6-digit OTP verification code has been sent to your email. Please check your inbox.')
      setAuthMode('otp_verify')
    } catch (err: any) {
      alert('Error: ' + err.message)
    }
  }

  // Handle OTP verification
  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!otpCode) return alert('Please enter the verification code.')

    try {
      const { error } = await supabase.auth.verifyOtp({
        email,
        token: otpCode,
        type: 'recovery'
      })

      if (error) {
        return alert('OTP Verification Failed: ' + error.message)
      }

      alert('OTP code verified successfully! Please enter your new secure password.')
      setAuthMode('reset_password')
    } catch (err: any) {
      alert('Error: ' + err.message)
    }
  }

  // Handle password updates
  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newPassword || !confirmNewPassword) return alert('Please enter and confirm your new password.')
    if (newPassword !== confirmNewPassword) return alert('Passwords do not match.')
    if (newPassword.length < 6) return alert('Password must be at least 6 characters long.')

    try {
      const { error } = await supabase.auth.updateUser({ password: newPassword })
      if (error) {
        return alert('Failed to update password: ' + error.message)
      }

      // Successful password change - clear out all attempts and lockouts!
      setForgotAttempts(0)
      setForgotAttemptsForEmail(email, 0)
      setForgotLockoutForEmail(email, 0)
      setForgotLockoutTime(0)

      setLoginAttempts(0)
      setLoginAttemptsForEmail(email, 0)
      setLoginLockoutForEmail(email, 0)
      setLoginLockoutTime(0)

      setOtpCode('')
      setNewPassword('')
      setConfirmNewPassword('')

      // Log out of the temporary recovery session so they can sign in cleanly
      await supabase.auth.signOut()

      alert('Password updated successfully! Please sign in with your new password.')
      setAuthMode('login')
    } catch (err: any) {
      alert('Error: ' + err.message)
    }
  }

  // Handle Resend Confirmation link
  const handleResendConfirmation = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!email) return alert('Please enter your email address.')

    try {
      const { error } = await supabase.auth.resend({
        type: 'signup',
        email: email,
        options: {
          emailRedirectTo: window.location.origin
        }
      })

      if (error) {
        return alert('Resend Failed: ' + error.message)
      }

      alert('A new confirmation link has been sent to your email address. Please check your inbox.')
    } catch (err: any) {
      alert('Error: ' + err.message)
    }
  }

  // Handle Auth submission
  const handleAuth = async (e: React.FormEvent) => {
    e.preventDefault()
    if (authMode === 'forgot') return handleForgotPassword(e)
    if (authMode === 'otp_verify') return handleVerifyOtp(e)
    if (authMode === 'reset_password') return handleResetPassword(e)
    if (authMode === 'unconfirmed_email') return handleResendConfirmation(e)
    if (!email || !password) return alert('Email and Password are required')

    // Add Email format validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailRegex.test(email)) return alert('Please enter a valid email address.')

    // Add Password length validation
    if (password.length < 6) return alert('Password must be at least 6 characters long.')

    if (authMode === 'signup') {
      if (!fullName || !companyName) {
        return alert('Please fill in Name and Company Name to create your MemoBud credentials.')
      }

      // Phone validation (7 to 15 digits)
      if (phoneNumber && (phoneNumber.length < 7 || phoneNumber.length > 15)) {
        return alert('Please enter a valid phone number (between 7 and 15 digits).')
      }

      try {
        // Sign up with Supabase Auth (highly secure, bcrypt-hashed on Supabase PostgreSQL side)
        const { data, error } = await supabase.auth.signUp({
          email,
          password,
          options: {
            data: {
              full_name: fullName,
              company_name: companyName,
              phone_number: phoneNumber ? (countryCode + ' ' + phoneNumber) : 'Not Registered Yet'
            }
          }
        })

        if (error) {
          return alert('Registration Failed: ' + error.message)
        }

        if (!data.user) {
          return alert('Registration Failed: No user data returned from the server.')
        }

        // Check if user already exists (Supabase returns an empty identities array for existing users during signup)
        if (data.user?.identities && data.user.identities.length === 0) {
          return alert('Registration Failed: An account with this email address already exists (perhaps you registered via Google?). Please go to "Secure Log In" to access your account.')
        }

        // Check if email confirmation is required (session is null)
        if (!data.session) {
          setAuthMode('unconfirmed_email')
          alert('A confirmation link has been sent to your email. Please click on the confirmation link to continue to your account.')
          return
        }

        const newUser = {
          email: data.user.email || email,
          name: fullName,
          company: companyName,
          phone: phoneNumber ? (countryCode + ' ' + phoneNumber) : 'Not Registered Yet',
          id: data.user.id
        }

        localStorage.setItem('memobud_user', JSON.stringify(newUser))
        setCurrentUser(newUser)
        setShowAuthModal(false)
        alert('Account created successfully! Welcome to MemoBud.')

        // Auto-reveal and scroll to the pricing/download section
        setShowPricingBelowApp(true)
        setTimeout(() => {
          const el = document.getElementById('licensing-anchor')
          if (el) el.scrollIntoView({ behavior: 'smooth' })
        }, 200)

        if (pendingAction) {
          processPendingAction(newUser)
        } else {
          fetchUserLicenses(email)
        }
      } catch (err: any) {
        alert('An unexpected error occurred during registration: ' + err.message)
      }
    } else {
      // 1. Check Login Lockout
      if (loginLockoutTime && Date.now() < loginLockoutTime) {
        const minutesLeft = Math.ceil((loginLockoutTime - Date.now()) / 60000)
        return alert(`Login attempts are temporarily disabled. Please try again in ${minutesLeft} minutes.`)
      }

      try {
        // Let's first explicitly check if the user is registered in our records or has an activation license.
        const { data: keysData } = await supabase
          .from('activation_keys')
          .select('id')
          .eq('email', email)
          .limit(1)

        const { data: profilesData } = await supabase
          .from('company_profiles')
          .select('email_id')
          .eq('email_id', email)
          .limit(1)

        // Standard Supabase Sign-in also verifies credentials securely
        const { data, error } = await supabase.auth.signInWithPassword({
          email,
          password
        })

        if (error) {
          // Check if it's an unconfirmed email error
          if (error.message.toLowerCase().includes("confirm")) {
            setAuthMode('unconfirmed_email')
            alert('Your email address has not been confirmed yet. Please check your inbox or request a new verification link below.')
            return
          }

          // If login fails, check if the email actually exists in either Supabase Auth or database keys
          if (error.status === 400 && (!keysData || keysData.length === 0) && (!profilesData || profilesData.length === 0)) {
            return alert('Login Failed: The email "' + email + '" is not registered in our client database. Please sign up for a new account.')
          }

          // Track Attempt
          let nextAttempts = loginAttempts + 1
          if (loginLockoutTime && Date.now() > loginLockoutTime) {
            // Lockout expired, reset attempts to 1
            nextAttempts = 1
            setLoginLockoutForEmail(email, 0)
            setLoginLockoutTime(0)
          }
          setLoginAttempts(nextAttempts)
          setLoginAttemptsForEmail(email, nextAttempts)

          if (nextAttempts >= 10) {
            const lockout = Date.now() + 15 * 60 * 1000
            setLoginLockoutForEmail(email, lockout)
            setLoginLockoutTime(lockout)
            return alert(`Too many failed login attempts. Login has been disabled for 15 minutes.`)
          }

          return alert(`Authentication Failed: ${error.message}`)
        }

        if (!data.user) {
          return alert('Authentication Failed: No user session established.')
        }

        // On successful login - clear out all attempts and lockouts!
        setForgotAttempts(0)
        setForgotAttemptsForEmail(email, 0)
        setForgotLockoutForEmail(email, 0)
        setForgotLockoutTime(0)

        setLoginAttempts(0)
        setLoginAttemptsForEmail(email, 0)
        setLoginLockoutForEmail(email, 0)
        setLoginLockoutTime(0)

        const metadata = data.user.user_metadata || {}
        const loggedUser = {
          email: data.user.email || email,
          name: metadata.full_name || email.split('@')[0].toUpperCase(),
          company: metadata.company_name || 'Partner Enterprise Corp',
          phone: metadata.phone_number || '+1 (555) 308-4822',
          id: data.user.id
        }

        localStorage.setItem('memobud_user', JSON.stringify(loggedUser))
        setCurrentUser(loggedUser)
        setShowAuthModal(false)

        setShowPricingBelowApp(true)
        setTimeout(() => {
          const el = document.getElementById('licensing-anchor')
          if (el) el.scrollIntoView({ behavior: 'smooth' })
        }, 200)

        if (pendingAction) {
          processPendingAction(loggedUser)
        } else {
          fetchUserLicenses(email)
        }
      } catch (err: any) {
        alert('An unexpected error occurred during login: ' + err.message)
      }
    }
  }

  // Handle Logout
  const handleLogout = async () => {
    if (!window.confirm('Are you sure you want to logout?')) return
    try {
      const { error } = await supabase.auth.signOut()
      if (error) {
        console.error('Supabase SignOut error:', error.message)
      }
    } catch (err) {
      console.error('Logout error:', err)
    }
    localStorage.removeItem('memobud_user')
    setCurrentUser(null)
    setUserKeys([])
    setSelectedKeyForDevices(null)
    setDevicesForKey([])
    setActiveTab('home')
  }

  // Handle Download Windows Installer action
  const handleDownloadInstallerClick = () => {
    if (!currentUser) {
      setPendingAction({ type: 'license', tier: 'trial' })
      setAuthMode('signup')
      setShowAuthModal(true)
    } else {
      setShowPricingBelowApp(true)
      setTimeout(() => {
        const el = document.getElementById('licensing-anchor')
        if (el) el.scrollIntoView({ behavior: 'smooth' })
      }, 150)
    }
  }

  // Route transaction actions
  const triggerAction = (type: 'license' | 'cloud', tier: any) => {
    if (type === 'cloud') {
      let reqSpaceMb = 0
      if (tier === '50mb_cloud') reqSpaceMb = 50
      else if (tier === '100mb_cloud') reqSpaceMb = 100
      else if (tier === '500mb_cloud') reqSpaceMb = 500
      else if (tier === '1gb_cloud') reqSpaceMb = 1000
      else if (tier === '3gb_cloud') reqSpaceMb = 3000

      if (!isDigitalOceanEnabled && reqSpaceMb > remainingFreeSpaceMb) {
        alert("Warning: Insufficient server allocation capacity on free tier. Please request support to expand database capacity.")
        return
      }
    }

    const actionObj = { type, tier }
    if (!currentUser) {
      setPendingAction(actionObj)
      setAuthMode('signup')
      setShowAuthModal(true)
    } else {
      executeAction(actionObj, currentUser)
    }
  }

  const processPendingAction = (userObj: any) => {
    if (!pendingAction) return
    executeAction(pendingAction, userObj)
    setPendingAction(null)
  }

  const executeAction = (actionObj: any, userObj: any) => {
    setPendingAction(actionObj)
    if (actionObj.tier === 'trial') {
      // Free trial does not require payment gateway card entry. Ask for Name, Email ID, Phone Number in beautiful modal.
      setTrialName(userObj.name || '')
      setTrialEmail(userObj.email || '')
      setTrialPhone(userObj.phone || '')
      setShowTrialFormModal(true)
    } else {
      // Set up step 1 Contact details
      setBillingName(userObj.name || '')
      setBillingEmail(userObj.email || '')
      setBillingPhone(userObj.phone || '')
      setCheckoutStep('contact')
      setRazorpayMethod(null)
      
      // Clear card fields
      setCardName(userObj.name || '')
      setCardNumber('')
      setCardExpiry('')
      setCardCvv('')
      setPaymentSuccessData(null)
      setShowPaymentModal(true)
    }
  }

  // Enforces Trial anti-abuse constraints and generates license key code
  const handleTrialGeneration = async (tName: string, tEmail: string, tPhone: string) => {
    if (!tName || !tEmail || !tPhone) {
      return alert('All fields (Name, Email ID, and Phone Number) are required to claim a free trial key.')
    }
    try {
      // 1. Enforce: single trial limit per user account profile email address
      const { data: existingKeys, error: checkError } = await supabase
        .from('activation_keys')
        .select('*')
        .eq('email', tEmail)
      
      if (checkError) console.error(checkError)
      
      const alreadyHasTrial = existingKeys?.some(k => k.is_trial === true)
      if (alreadyHasTrial) {
        alert('Trial Activation Failed: This email has already claimed a 7-day free trial key. You can only activate one of our tier plans (Basic, Professional, or Enterprise).')
        return
      }

      // Generate a functional 29-character activation code
      const segments = Array.from({ length: 4 }, () => Math.random().toString(36).substring(2, 7).toUpperCase())
      const generatedCode = 'MB-TRIAL-' + segments.join('-')
      const expirationDate = new Date()
      expirationDate.setDate(expirationDate.getDate() + 7)

      const keyPayload = {
        key_code: generatedCode,
        email: tEmail,
        subscription_id: BASIC_SUB_ID,
        is_active: true,
        is_trial: true,
        expires_at: expirationDate.toISOString(),
        cloud_sync_enabled: true,
        cloud_storage_limit_gb: 2,
        cloud_storage_used_mb: 0,
        custom_max_devices: 1
      }

      const { data: keyData, error: keyError } = await supabase
        .from('activation_keys')
        .insert([keyPayload])
        .select()

      if (keyError) {
        alert('Failed to generate trial license code: ' + keyError.message)
      } else {
        const newKeyId = keyData[0].id
        
        // Write comprehensive user metadata parameters (phone number, company name, email id) into company_profiles
        const profilePayload = {
          activation_key_id: newKeyId,
          company_name: currentUser?.company || companyName || 'MemoBud Sandbox Partner',
          phone_number: tPhone,
          email_id: tEmail
        }
        await supabase.from('company_profiles').insert([profilePayload])

        // Keep current authenticated state synchronized
        const updatedUser = {
          ...currentUser,
          name: tName,
          email: tEmail,
          phone: tPhone
        }
        setCurrentUser(updatedUser)
        localStorage.setItem('memobud_user', JSON.stringify(updatedUser))

        setPaymentSuccessData({
          keyCode: generatedCode,
          tierName: '7-Day Free Trial',
          expiresAt: expirationDate.toLocaleDateString(),
          isTrial: true
        })
        
        // Close Trial Form Modal and open the Payment/Receipt success Modal
        setShowTrialFormModal(false)
        setShowPaymentModal(true)
        fetchUserLicenses(tEmail)
      }
    } catch (e: any) {
      alert('Request error: ' + e.message)
    }
  }

  const handleProceedToRazorpay = (e: React.FormEvent) => {
    e.preventDefault()
    if (!billingName || !billingEmail || !billingPhone) {
      return alert('Please fill in your Name, Email ID, and Phone Number to proceed.')
    }
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailRegex.test(billingEmail)) {
      return alert('Please enter a valid email address.')
    }
    if (billingPhone.replace(/\D/g, '').length < 7) {
      return alert('Please enter a valid phone number (at least 7 digits).')
    }
    setCheckoutStep('razorpay')
  }

  // Handle Mock Payment Submission
  const submitMockPayment = async (e?: React.FormEvent) => {
    if (e) e.preventDefault()
    
    if (razorpayMethod === 'card') {
      if (!cardNumber || !cardExpiry || !cardCvv) {
        return alert('Please fill in card details to complete the payment.')
      }
    } else if (!razorpayMethod) {
      return alert('Please select a payment method in RazorPay.')
    }
    
    setIsProcessingPayment(true)
    
    // Simulate premium banking verification delay
    setTimeout(async () => {
      try {
        if (pendingCloudUpgrade) {
          // Process Cloud Sync Upgrade Payment securely!
          const { error } = await supabase
            .from('activation_keys')
            .update({
              cloud_sync_enabled: true,
              cloud_storage_limit_gb: pendingCloudUpgrade.gb,
              custom_max_devices: pendingCloudUpgrade.devices,
              cloud_storage_used_mb: 0
            })
            .eq('id', pendingCloudUpgrade.keyId);

          if (error) {
            alert('Cloud upgrade payment failed: ' + error.message);
          } else {
            const upgradedKey = userKeys.find(k => k.id === pendingCloudUpgrade.keyId);
            setPaymentSuccessData({
              keyCode: upgradedKey?.key_code || pendingCloudUpgrade.keyId,
              tierName: pendingCloudUpgrade.planName,
              expiresAt: upgradedKey?.expires_at ? new Date(upgradedKey.expires_at).toLocaleDateString() : new Date().toLocaleDateString(),
              isTrial: false
            });
            if (currentUser) {
              await fetchUserLicenses(currentUser.email);
            }
          }
          setIsProcessingPayment(false);
          return;
        }

        if (pendingPlanUpgrade) {
          // Process main License Plan Upgrade securely!
          let upgradedSubId = BASIC_SUB_ID
          let maxSeats = 3
          if (pendingPlanUpgrade.tier === 'pro') {
            upgradedSubId = PRO_SUB_ID
            maxSeats = 10
          } else if (pendingPlanUpgrade.tier === 'enterprise') {
            upgradedSubId = ENTERPRISE_SUB_ID
            maxSeats = 25
          } else if (pendingPlanUpgrade.tier === 'unlimited') {
            upgradedSubId = 'unl-' + Math.random().toString(36).substring(2,7)
            maxSeats = 9999
          }

          // 1 year validity from now
          const expirationDate = new Date()
          expirationDate.setDate(expirationDate.getDate() + 365)

          const { error } = await supabase
            .from('activation_keys')
            .update({
              is_trial: false,
              subscription_id: upgradedSubId,
              custom_max_devices: maxSeats,
              expires_at: expirationDate.toISOString()
            })
            .eq('id', pendingPlanUpgrade.keyId);

          if (error) {
            alert('License plan upgrade payment failed: ' + error.message);
          } else {
            const upgradedKey = userKeys.find(k => k.id === pendingPlanUpgrade.keyId);
            setPaymentSuccessData({
              keyCode: upgradedKey?.key_code || pendingPlanUpgrade.keyId,
              tierName: pendingPlanUpgrade.planName,
              expiresAt: expirationDate.toLocaleDateString(),
              isTrial: false
            });
            if (currentUser) {
              await fetchUserLicenses(currentUser.email);
            }
          }
          setIsProcessingPayment(false);
          return;
        }

        if (!pendingAction && !paymentSuccessData) {
          setIsProcessingPayment(false)
          setShowPaymentModal(false)
          return
        }

        const currentAction = pendingAction || { type: 'license', tier: 'basic' }
        const segments = Array.from({ length: 4 }, () => Math.random().toString(36).substring(2, 7).toUpperCase())
        
        let generatedCode = ''
        let tierName = ''
        let subId = BASIC_SUB_ID
        let maxSeats = 3
        let cloudEnabled = false
        let cloudLimit = 0
        let expirationDate = new Date()
        if (currentAction.type === 'license') {
          expirationDate.setFullYear(expirationDate.getFullYear() + 100) // Lifetime standalone local license (One-time payment)
        } else {
          expirationDate.setDate(expirationDate.getDate() + 30) // 1 month subscription for cloud
        }

        if (currentAction.type === 'license') {
          if (currentAction.tier && typeof currentAction.tier === 'object') {
            generatedCode = 'MB-CST-LCL-' + segments.join('-')
            maxSeats = currentAction.tier.devices
            tierName = currentAction.tier.name || `Custom Local (${maxSeats} Devices)`
            subId = BASIC_SUB_ID
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'starter_local') {
            generatedCode = 'MB-STR-' + segments.join('-')
            tierName = 'Starter Local'
            subId = BASIC_SUB_ID
            maxSeats = 1
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'standard_local') {
            generatedCode = 'MB-STD-' + segments.join('-')
            tierName = 'Standard Local'
            subId = BASIC_SUB_ID
            maxSeats = 3
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'pro_local') {
            generatedCode = 'MB-PRO-' + segments.join('-')
            tierName = 'Professional Local'
            subId = PRO_SUB_ID
            maxSeats = 10
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'basic') {
            generatedCode = 'MB-BSC-' + segments.join('-')
            tierName = 'Basic Local'
            subId = BASIC_SUB_ID
            maxSeats = 3
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'pro') {
            generatedCode = 'MB-PRO-' + segments.join('-')
            tierName = 'Professional Local'
            subId = PRO_SUB_ID
            maxSeats = 10
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'enterprise') {
            generatedCode = 'MB-ENT-' + segments.join('-')
            tierName = 'Enterprise Local'
            subId = ENTERPRISE_SUB_ID
            maxSeats = 25
            cloudEnabled = false
            cloudLimit = 0
          } else if (currentAction.tier === 'unlimited') {
            generatedCode = 'MB-UNL-' + segments.join('-')
            tierName = 'Unlimited Local'
            subId = 'unl-' + Math.random().toString(36).substring(2,7)
            maxSeats = 9999
            cloudEnabled = false
            cloudLimit = 0
          }

          const keyPayload = {
            key_code: generatedCode,
            email: billingEmail || currentUser?.email,
            subscription_id: subId,
            is_active: true,
            is_trial: false,
            expires_at: expirationDate.toISOString(),
            cloud_sync_enabled: cloudEnabled,
            cloud_storage_limit_gb: cloudLimit,
            cloud_storage_used_mb: 0,
            custom_max_devices: maxSeats
          }

          const { data: keyData, error: keyError } = await supabase
            .from('activation_keys')
            .insert([keyPayload])
            .select()

          if (keyError) {
            alert('Failed to register subscription: ' + keyError.message)
          } else {
            const newKeyId = keyData[0].id
            const profilePayload = {
              activation_key_id: newKeyId,
              company_name: currentUser?.company || 'Enterprise Partner',
              phone_number: billingPhone || currentUser?.phone || 'No Phone Number Supplied',
              email_id: billingEmail || currentUser?.email
            }
            await supabase.from('company_profiles').insert([profilePayload])

            // Update current user details so they match what was submitted, but preserve email for active session
            const updatedUser = {
              ...currentUser,
              phone: billingPhone || currentUser?.phone
            }
            setCurrentUser(updatedUser)
            localStorage.setItem('memobud_user', JSON.stringify(updatedUser))

            setPaymentSuccessData({
              keyCode: generatedCode,
              tierName: tierName,
              expiresAt: expirationDate.toLocaleDateString(),
              isTrial: false
            })
            if (currentUser) {
              fetchUserLicenses(currentUser.email)
            }
          }
        } else if (currentAction.type === 'cloud') {
          let storageLimit = 0.05
          let priceName = ''
          if (currentAction.tier === '50mb_cloud') {
            storageLimit = 0.05
            priceName = '50MB Cloud Sync (3 Devices, 50MB)'
            maxSeats = 3
          } else if (currentAction.tier === '100mb_cloud') {
            storageLimit = 0.1
            priceName = '100MB Cloud Sync (5 Devices, 100MB)'
            maxSeats = 5
          } else if (currentAction.tier === '500mb_cloud') {
            storageLimit = 0.5
            priceName = '500MB Cloud Sync (10 Devices, 500MB)'
            maxSeats = 10
          } else if (currentAction.tier === '1gb_cloud') {
            storageLimit = 1.0
            priceName = '1GB Cloud Sync (15 Devices, 1GB)'
            maxSeats = 15
          } else if (currentAction.tier === '3gb_cloud') {
            storageLimit = 3.0
            priceName = '3GB Cloud Sync (Unlimited Devices, 3GB)'
            maxSeats = 9999
          } else if (currentAction.tier && typeof currentAction.tier === 'object') {
            storageLimit = currentAction.tier.storage
            maxSeats = currentAction.tier.devices
            priceName = currentAction.tier.name || `Custom Cloud (${maxSeats} Devices, ${storageLimit} GB)`
          }

          // Fetch user's latest key to update cloud sync allocation in Supabase!
          if (userKeys.length > 0) {
            const activeKeyObj = userKeys[0]
            const cloudExpires = new Date()
            cloudExpires.setDate(cloudExpires.getDate() + 30) // 30-day billing cycle activation

            const { error: updateError } = await supabase
              .from('activation_keys')
              .update({
                cloud_sync_enabled: true,
                cloud_storage_limit_gb: storageLimit,
                custom_max_devices: maxSeats,
                is_trial: false, // Upgrade from trial if active
                expires_at: cloudExpires.toISOString()
              })
              .eq('id', activeKeyObj.id)

            if (updateError) {
              alert('Cloud setup failed: ' + updateError.message)
            } else {
              setPaymentSuccessData({
                keyCode: activeKeyObj.key_code,
                tierName: priceName,
                expiresAt: cloudExpires.toLocaleDateString(),
                isTrial: false
              })
              if (currentUser) {
                fetchUserLicenses(currentUser.email)
              }
            }
          } else {
            // If they don't have a key, issue a base key with cloud storage!
            generatedCode = 'MB-CLD-' + segments.join('-')
            const keyPayload = {
              key_code: generatedCode,
              email: billingEmail || currentUser?.email,
              subscription_id: BASIC_SUB_ID,
              is_active: true,
              is_trial: false,
              expires_at: expirationDate.toISOString(),
              cloud_sync_enabled: true,
              cloud_storage_limit_gb: storageLimit,
              cloud_storage_used_mb: 0,
              custom_max_devices: maxSeats
            }
            const { data: keyData, error: keyError } = await supabase
              .from('activation_keys')
              .insert([keyPayload])
              .select()

            if (!keyError) {
              const newKeyId = keyData[0].id
              await supabase.from('company_profiles').insert([{
                activation_key_id: newKeyId,
                company_name: currentUser?.company || 'Enterprise Partner',
                phone_number: billingPhone || currentUser?.phone || 'No Phone Number Supplied',
                email_id: billingEmail || currentUser?.email
              }])
            }
            setPaymentSuccessData({
              keyCode: generatedCode,
              tierName: priceName,
              expiresAt: expirationDate.toLocaleDateString(),
              isTrial: false
            })
            if (currentUser) {
              fetchUserLicenses(currentUser.email)
            }
          }
        }
      } catch (err: any) {
        alert('Transaction error: ' + err.message)
      } finally {
        setIsProcessingPayment(false)
      }
    }, 1500)
  }

  // Remove a registered device to free up activation slots
  const disconnectDevice = async (deviceId: string) => {
    if (!window.confirm('Are you sure you want to disconnect this device seat? This will instantly trigger logout on that client.')) return
    try {
      // 1. Update all service_memos referencing this device_id to null to prevent foreign key violations
      const { error: updateError } = await supabase
        .from('service_memos')
        .update({ device_id: null })
        .eq('device_id', deviceId)

      if (updateError) {
        return alert('Failed to clear device references from service memos: ' + updateError.message)
      }

      // 2. Now delete the device seat safely
      const { error } = await supabase
        .from('devices')
        .delete()
        .eq('id', deviceId)

      if (error) {
        alert('Failed to disconnect device: ' + error.message)
      } else if (currentUser) {
        await fetchUserLicenses(currentUser.email)
      }
    } catch (e: any) {
      alert('Error: ' + e.message)
    }
  }

  // Handle user password update securely via old password validation
  const handleUpdatePasswordSettings = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!oldPasswordSettings || !newPasswordSettings || !confirmNewPasswordSettings) {
      alert('Error: Please fill out all password fields.')
      return
    }
    if (newPasswordSettings !== confirmNewPasswordSettings) {
      alert('Error: New password and confirmation do not match.')
      return
    }
    if (newPasswordSettings.length < 6) {
      alert('Error: New password must be at least 6 characters long.')
      return
    }

    setIsChangingPasswordSettings(true)
    try {
      // 1. Re-authenticate user to verify their old password
      const { error: signInError } = await supabase.auth.signInWithPassword({
        email: currentUser.email,
        password: oldPasswordSettings
      })

      if (signInError) {
        alert('Failed to update password: Old password is incorrect.')
        setIsChangingPasswordSettings(false)
        return
      }

      // 2. Perform the update
      const { error: updateError } = await supabase.auth.updateUser({
        password: newPasswordSettings
      })

      if (updateError) {
        alert('Failed to update password: ' + updateError.message)
      } else {
        alert('Success: Password updated successfully!')
        setOldPasswordSettings('')
        setNewPasswordSettings('')
        setConfirmNewPasswordSettings('')
      }
    } catch (err: any) {
      alert('Error changing password: ' + err.message)
    } finally {
      setIsChangingPasswordSettings(false)
    }
  }

  // Handle clearing all cloud data for a specific activation key
  const handleClearKeyDataSettings = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedKeyToClear) {
      alert('Error: Please select an activation key to clear.')
      return
    }

    // Find the chosen activation key object
    const targetKey = userKeys.find(k => k.id === selectedKeyToClear)
    if (!targetKey) {
      alert('Error: Selected activation key is invalid.')
      return
    }

    const keyCode = targetKey.key_code
    const confirmMessage = `WARNING: Are you sure you want to permanently clear all data for the activation key "${keyCode}"?\n\nThis will take a local backup inside "Documents/joborgen/service memo generator/backups", wipe your local database copy, and permanently delete all cloud backups for this key. This action is IRREVERSIBLE!`
    if (!window.confirm(confirmMessage)) return

    const secondaryConfirm = window.prompt(`Please type the activation key "${keyCode}" to confirm deletion:`)
    if (secondaryConfirm !== keyCode) {
      alert('Verification failed. Data clearance cancelled.')
      return
    }

    setIsClearingDataSettings(true)
    setClearStep(1) // Step 1: Connecting to desktop app
    
    // Artificial small delay for smooth visual transition
    await new Promise(resolve => setTimeout(resolve, 800))

    try {
      // 1. Request local client backup & wipe first via 127.0.0.1 (explicit IPv4 to prevent local DNS/IPv6 resolution issues)
      try {
        const localRes = await fetch('http://127.0.0.1:14010/api/clear-workspace', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({ keyCode })
        })

        if (!localRes.ok) {
          const errData = await localRes.json().catch(() => ({}))
          throw new Error(errData.error || `Local server returned status ${localRes.status}`)
        }
      } catch (localErr: any) {
        // If the C# desktop app is not running or blocked, abort
        alert(
          `Clearance Aborted: Could not connect to the local Service Memo App to take a backup and clear local data.\n\n` +
          `Error Details: ${localErr.message}\n\n` +
          `RECOMMENDED ACTIONS:\n` +
          `1. Ensure the desktop app is open and running on this computer.\n` +
          `2. If the app is open but you still see this, the port binding may be blocked by system permissions. Please try closing the desktop app, opening your terminal / command prompt as Administrator, and running the app there (e.g. dotnet run), or right-click the desktop app executable and select "Run as Administrator", then click the Clear Data button again.`
        )
        setClearStep(0)
        setIsClearingDataSettings(false)
        return
      }

      setClearStep(2) // Step 2: Creating local JSON backup & clearing database
      await new Promise(resolve => setTimeout(resolve, 1000))

      setClearStep(3) // Step 3: Deleting cloud records
      await new Promise(resolve => setTimeout(resolve, 500))

      // 2. Fetch all devices associated with this key
      const { data: devices, error: devError } = await supabase
        .from('devices')
        .select('id')
        .eq('activation_key_id', selectedKeyToClear)

      if (devError) throw devError

      const deviceIds = devices?.map((d: any) => d.id) || []

      // 3. Delete service memos linked to these device IDs in cloud
      if (deviceIds.length > 0) {
        const { error: delError1 } = await supabase
          .from('service_memos')
          .delete()
          .in('device_id', deviceIds)

        if (delError1) throw delError1
      }

      // 4. Delete service memos matching this keyCode in json_data in cloud
      const { error: delError2 } = await supabase
        .from('service_memos')
        .delete()
        .filter('json_data->>CloudOwnerKey', 'eq', keyCode)

      if (delError2) throw delError2

      setClearStep(4) // Step 4: Successfully completed!
      await new Promise(resolve => setTimeout(resolve, 1500))

      setSelectedKeyToClear('')
    } catch (err: any) {
      alert('Failed to clear data: ' + err.message)
    } finally {
      setClearStep(0)
      setIsClearingDataSettings(false)
    }
  }

  // Toggle cloud sync option directly inside user console dashboard
  const handleToggleCloudSync = async (keyId: string, currentEnabled: boolean) => {
    try {
      const nextEnabled = !currentEnabled
      const nextLimit = nextEnabled ? 5 : 0 // Enable with 5GB default limit, or 0GB if disabled
      const { error } = await supabase
        .from('activation_keys')
        .update({
          cloud_sync_enabled: nextEnabled,
          cloud_storage_limit_gb: nextLimit
        })
        .eq('id', keyId)

      if (error) {
        alert('Failed to update cloud sync config: ' + error.message)
      } else {
        if (currentUser) {
          await fetchUserLicenses(currentUser.email)
        }
      }
    } catch (e: any) {
      alert('Error updating cloud sync status: ' + e.message)
    }
  }

  // Handle open upgrade modal for Cloud Sync
  const handleUpgradeCloudSync = (keyId: string) => {
    setSelectedUpgradeKeyId(keyId)
    setShowCloudUpgradeModal(true)
  }

  // Set up pending upgrade and redirect to payment gateway
  const handleSelectUpgradePlan = (keyId: string, gb: number, planName: string, price: number, devices: number) => {
    setPendingCloudUpgrade({ keyId, gb, price, planName, devices })
    setShowCloudUpgradeModal(false)
    
    // Set up step 1 Contact details
    setBillingName(currentUser?.name || '')
    setBillingEmail(currentUser?.email || '')
    setBillingPhone(currentUser?.phone || '')
    setCheckoutStep('contact')
    setRazorpayMethod(null)
    setPaymentSuccessData(null)
    
    // Clear card fields
    setCardName(currentUser?.name || '')
    setCardNumber('')
    setCardExpiry('')
    setCardCvv('')
    
    // Open payment gateway
    setShowPaymentModal(true)
  }

  const handleSelectPlanUpgrade = (keyId: string, tier: 'basic' | 'pro' | 'enterprise' | 'unlimited', planName: string, price: number) => {
    setPendingPlanUpgrade({ keyId, tier, planName, price })
    setShowPlanUpgradeModal(false)
    
    // Set up step 1 Contact details
    setBillingName(currentUser?.name || '')
    setBillingEmail(currentUser?.email || '')
    setBillingPhone(currentUser?.phone || '')
    setCheckoutStep('contact')
    setRazorpayMethod(null)
    setPaymentSuccessData(null)
    
    // Clear card fields
    setCardName(currentUser?.name || '')
    setCardNumber('')
    setCardExpiry('')
    setCardCvv('')
    
    // Open payment gateway
    setShowPaymentModal(true)
  }

  /*
  // Execute the actual Cloud Sync upgrade with selected storage limit
  const executeCloudUpgrade = async (keyId: string, gb: number) => {
    if (!gb || gb <= 0) return alert("Invalid storage amount selected.")
    setIsProcessingUpgrade(true)
    
    try {
      const { error } = await supabase
        .from('activation_keys')
        .update({
          cloud_sync_enabled: true,
          cloud_storage_limit_gb: gb,
          cloud_storage_used_mb: 0
        })
        .eq('id', keyId);

      if (error) {
        alert('Failed to upgrade cloud sync config: ' + error.message);
      } else {
        alert(`Successfully upgraded to Cloud Sync! Your key has been allocated ${gb} GB of cloud backup storage.`);
        setShowCloudUpgradeModal(false);
        setSelectedUpgradeKeyId(null);
        if (currentUser) {
          await fetchUserLicenses(currentUser.email);
        }
      }
    } catch (e: any) {
      alert('Error updating cloud sync status: ' + e.message)
    } finally {
      setIsProcessingUpgrade(false)
    }
  }
  */

  // Handle cell style customization in live playground
  const updateCellColor = (property: 'bg' | 'color', value: string) => {
    setCanvasStyles(prev => ({
      ...prev,
      [selectedCell]: {
        ...prev[selectedCell as keyof typeof prev],
        [property]: value
      }
    }))
  }

  const toggleCellBold = () => {
    setCanvasStyles(prev => ({
      ...prev,
      [selectedCell]: {
        ...prev[selectedCell as keyof typeof prev],
        bold: !prev[selectedCell as keyof typeof prev].bold
      }
    }))
  }

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      
      {/* HEADER NAVIGATION SECTION */}
      {activeTab !== 'dashboard' && (
      <header className="header">
        <div className="container nav-bar">
          <div className="logo-container" onClick={() => { setActiveTab('home'); setProductView('list'); }}>
            <div style={{ width: 40, height: 40, borderRadius: 10, background: 'linear-gradient(135deg, var(--primary) 0%, var(--primary-container) 100%)', display: 'flex', alignItems: 'center', justifySelf: 'center', justifyContent: 'center', color: '#fff', fontWeight: 800, fontSize: 18 }}>MB</div>
            <span className="logo-text">MemoBud</span>
          </div>

          <nav className="nav-links">
            <button className={`nav-link ${activeTab === 'home' ? 'active' : ''}`} onClick={() => { setActiveTab('home'); setProductView('list'); }}>Home</button>
            <button className={`nav-link ${activeTab === 'products' ? 'active' : ''}`} onClick={() => { setActiveTab('products'); setProductView('list'); }}>Products</button>
            <button className={`nav-link ${activeTab === 'company' ? 'active' : ''}`} onClick={() => { setActiveTab('company'); setProductView('list'); }}>Company</button>
          </nav>

          <div className="nav-ctas">
            <button 
              onClick={() => setIsDarkMode(!isDarkMode)} 
              style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--on-surface-variant)', display: 'flex', padding: 8, borderRadius: '50%' }}
              title="Toggle Theme"
            >
              {isDarkMode ? <Sun size={20} /> : <Moon size={20} />}
            </button>

            {currentUser ? (
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span className="caption" style={{ fontWeight: 600, fontSize: '0.9rem', color: 'var(--primary)' }}>{currentUser.email}</span>
                <button 
                  className="btn btn-primary" 
                  onClick={() => { setActiveTab('dashboard'); setSelectedDashboardProduct('generator'); }}
                  style={{ padding: '8px 16px', fontSize: '0.9rem' }}
                >
                  User Dashboard
                </button>
                <button className="btn btn-outline" style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: 6 }} onClick={handleLogout}>
                  <LogOut size={16} /> Logout
                </button>
              </div>
            ) : (
              <button className="btn btn-primary" onClick={() => { setAuthMode('login'); setShowAuthModal(true); setPendingAction(null); }}>
                Client Dashboard
              </button>
            )}
          </div>
        </div>
      </header>
      )}

      {/* RENDER DYNAMIC PAGE VIEWS */}
      
      {/* 1. HOME LANDING PAGE (MEMOBUD COMPANY PLATFORM) */}
      {activeTab === 'home' && (
        <main style={{ flex: 1 }}>
          
          {/* Hero Banner Area */}
          <section className="hero-section">
            <div className="container hero-grid">
              <div className="hero-content">
                <div className="hero-badge"><Sparkles size={14} style={{ marginRight: 6 }} /> Integrated Managerial Suite</div>
                <h1 className="hero-headline">Professional Workspace Suite for Business Operations.</h1>
                <p className="hero-tagline">
                  MemoBud builds clean, high-density programs to automate administrative and managerial bottlenecks. Coordinate invoicing, billing, job orders, and service sheets with authoritative structural layouts.
                </p>
                <div className="hero-ctas">
                  <button className="btn btn-primary" onClick={() => { setActiveTab('products'); setProductView('list'); }}>
                    Our Product Suite <ChevronRight size={18} />
                  </button>
                  <button className="btn btn-secondary" onClick={handleDownloadInstallerClick} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <Download size={16} /> Download Windows Installer
                  </button>
                </div>
              </div>

              <div className="hero-visual">
                <div className="hero-glow"></div>
                {/* Showcase Interactive Canvas */}
                <div className="canvas-mockup glass-panel">
                  <div className="mockup-header">
                    <div className="mockup-dots">
                      <span className="mockup-dot red"></span>
                      <span className="mockup-dot yellow"></span>
                      <span className="mockup-dot green"></span>
                    </div>
                    <span className="mockup-title">MemoBud Workspace (Simulated Canvas)</span>
                    <span style={{ fontSize: 10, color: 'var(--primary)', fontWeight: 'bold' }}>LIVE PREVIEW</span>
                  </div>
                  
                  <div className="mockup-body" style={{ padding: 20 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12 }}>
                      <div>
                        <div style={{ fontSize: 18, fontWeight: 800, color: 'var(--primary)' }}>MemoBud</div>
                        <div style={{ fontSize: 10, color: 'var(--on-surface-variant)' }}>Administrative Billing Portal</div>
                      </div>
                      <div style={{ textAlign: 'right' }}>
                        <div style={{ fontSize: 12, fontWeight: 700 }}>INVOICE SHEET</div>
                        <div style={{ fontSize: 10, fontFamily: 'monospace' }}>#901-MEMO-2026</div>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr 90px', gap: 2, background: 'var(--surface-container-high)', padding: 1, borderRadius: 6, overflow: 'hidden' }}>
                      {/* Interactive Header Cell */}
                      <div 
                        onClick={() => setSelectedCell('header')}
                        style={{ 
                          gridColumn: '1 / -1', 
                          padding: '10px 14px', 
                          textAlign: 'center', 
                          fontSize: 12,
                          background: canvasStyles.header.bg, 
                          color: canvasStyles.header.color, 
                          fontWeight: canvasStyles.header.bold ? 'bold' : 'normal',
                          cursor: 'pointer',
                          transition: 'all 0.2s'
                        }}
                      >
                        MANAGERIAL BILLING &amp; JOB ORDER OUTLINE
                      </div>

                      {/* Content cells */}
                      <div style={{ background: 'var(--surface-container-low)', padding: '8px 12px', fontSize: 10, fontWeight: 'bold' }}>QTY</div>
                      <div style={{ background: 'var(--surface-container-low)', padding: '8px 12px', fontSize: 10, fontWeight: 'bold' }}>DESCRIPTION</div>
                      <div style={{ background: 'var(--surface-container-low)', padding: '8px 12px', fontSize: 10, fontWeight: 'bold', textAlign: 'right' }}>PRICE</div>

                      <div 
                        onClick={() => setSelectedCell('qty')}
                        style={{ 
                          background: canvasStyles.qty.bg, 
                          color: canvasStyles.qty.color, 
                          fontWeight: canvasStyles.qty.bold ? 'bold' : 'normal',
                          padding: '10px 12px', 
                          fontSize: 11,
                          cursor: 'pointer'
                        }}
                      >
                        01 System
                      </div>
                      <div 
                        onClick={() => setSelectedCell('desc')}
                        style={{ 
                          background: canvasStyles.desc.bg, 
                          color: canvasStyles.desc.color, 
                          fontWeight: canvasStyles.desc.bold ? 'bold' : 'normal',
                          padding: '10px 12px', 
                          fontSize: 11,
                          cursor: 'pointer'
                        }}
                      >
                        Administrative Billing Module (Subscription License)
                      </div>
                      <div 
                        onClick={() => setSelectedCell('price')}
                        style={{ 
                          background: canvasStyles.price.bg, 
                          color: canvasStyles.price.color, 
                          fontWeight: canvasStyles.price.bold ? 'bold' : 'normal',
                          padding: '10px 12px', 
                          fontSize: 11, 
                          textAlign: 'right',
                          cursor: 'pointer'
                        }}
                      >
                        $39.00
                      </div>
                    </div>

                    <div style={{ border: '1px dashed var(--outline)', borderRadius: 8, padding: 12, marginTop: 12, display: 'flex', flexDirection: 'column', gap: 6 }}>
                      <div className="caption" style={{ fontSize: 10, fontWeight: 'bold', textTransform: 'uppercase', color: 'var(--primary)' }}>Live Styles Panel</div>
                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                        <span style={{ fontSize: 10 }}>Selected: <strong style={{ textTransform: 'capitalize' }}>{selectedCell}</strong></span>
                        <div style={{ display: 'flex', gap: 4 }}>
                          <button 
                            onClick={() => updateCellColor('bg', '#004f96')} 
                            style={{ width: 16, height: 16, borderRadius: '50%', background: '#004f96', border: 'none', cursor: 'pointer' }}
                          />
                          <button 
                            onClick={() => updateCellColor('bg', '#10b981')} 
                            style={{ width: 16, height: 16, borderRadius: '50%', background: '#10b981', border: 'none', cursor: 'pointer' }}
                          />
                          <button 
                            onClick={() => updateCellColor('bg', '#f59e0b')} 
                            style={{ width: 16, height: 16, borderRadius: '50%', background: '#f59e0b', border: 'none', cursor: 'pointer' }}
                          />
                          <button 
                            onClick={() => updateCellColor('bg', '#ffffff')} 
                            style={{ width: 16, height: 16, borderRadius: '50%', background: '#ffffff', border: '1px solid #717783', cursor: 'pointer' }}
                          />
                        </div>
                        <button 
                          onClick={toggleCellBold} 
                          style={{ padding: '2px 6px', fontSize: 9, background: 'var(--surface-container-high)', border: 'none', borderRadius: 4, cursor: 'pointer', fontWeight: 'bold' }}
                        >
                          Bold
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* Managerial Task Features list */}
          <section style={{ padding: '80px 0', borderTop: '1px solid var(--outline-variant)' }}>
            <div className="container">
              <div className="section-header" style={{ maxWidth: 700, margin: '0 auto 60px auto', textAlign: 'center' }}>
                <div className="hero-badge">Ecosystem Capabilities</div>
                <h2>A Complete Toolkit for Business Operations</h2>
                <p>MemoBud replaces fragmented files with highly aligned digital sheets built for enterprise execution.</p>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 32, maxWidth: 900, margin: '0 auto' }}>
                
                <div className="card" style={{ gap: 14 }}>
                  <div style={{ width: 44, height: 44, borderRadius: 10, background: 'var(--secondary-container)', color: 'var(--primary)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Layers size={22} />
                  </div>
                  <h3>Invoice &amp; Billing Sheets</h3>
                  <p style={{ fontSize: '0.95rem' }}>Draft custom financial files, log client records, and generate high-density, professional statements without manual spreadsheet alignment.</p>
                </div>

                <div className="card" style={{ gap: 14 }}>
                  <div style={{ width: 44, height: 44, borderRadius: 10, background: 'var(--secondary-container)', color: 'var(--primary)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <Monitor size={22} />
                  </div>
                  <h3>Service Memo Generator</h3>
                  <p style={{ fontSize: '0.95rem' }}>Our flagship program built to outline machinery work orders, labor estimations, and precise field diagnostics in high-contrast prints.</p>
                  <button className="btn btn-outline" style={{ width: 'fit-content', padding: '6px 14px', fontSize: 12, marginTop: 8 }} onClick={() => { setActiveTab('products'); setSelectedProduct('generator'); setProductView('details'); }}>
                    View Flagship App
                  </button>
                </div>

              </div>
            </div>
          </section>

          {/* Brand Philosophy Segment */}
          <section style={{ padding: '80px 0', background: 'var(--surface-container-low)' }}>
            <div className="container">
              <div className="interactive-playground" style={{ background: 'var(--surface-container-lowest)' }}>
                <div className="playground-grid">
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <div className="hero-badge">Architectural Design</div>
                    <h2>The "No-Line" Formatting Paradigm</h2>
                    <p style={{ fontSize: '1rem', lineHeight: 1.6 }}>
                      Traditional programs rely on chaotic, messy spreadsheet outlines that create cognitive fatigue. MemoBud structures complex data through soft background elevation shifts and structural gray tones. The result is clean, professional memos that command authority.
                    </p>
                    <div style={{ padding: '12px 16px', background: 'var(--surface-container-low)', borderRadius: 8, display: 'flex', alignItems: 'center', gap: 10, fontSize: 13 }}>
                      <CheckCircle size={18} style={{ color: '#10b981' }} />
                      <span>Optimized dynamically in the desktop rendering engine.</span>
                    </div>
                  </div>

                  <div className="editor-sidebar" style={{ background: 'var(--surface-container-low)' }}>
                    <div className="editor-label">Enterprise Ready</div>
                    <h3>Security First</h3>
                    <p style={{ fontSize: '0.9rem' }}>
                      All registered user accounts operate on secure cryptographic pathways. Seat counts, hardware links, and database keys are tightly monitored by the MemoBud Cloud Admin server.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </section>

        </main>
      )}

      {/* 2. PRODUCTS TAB & DEDICATED APP DETAILS VIEW */}
      {activeTab === 'products' && (
        <main style={{ flex: 1 }}>
          
          {productView === 'list' ? (
            <section style={{ padding: '80px 0' }}>
              <div className="container">
                <div className="section-header" style={{ textAlign: 'center', marginBottom: 60 }}>
                  <div className="hero-badge">Products Suite</div>
                  <h2>Explore the MemoBud Ecosystem</h2>
                  <p>Sophisticated programs built to handle business administrative operations and billing logs with extreme visual alignment.</p>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 32, maxWidth: 960, margin: '0 auto' }}>
                  
                  {/* Service Memo & Job Order Generator Card */}
                  <div className="card glass-panel" style={{ padding: 36, display: 'flex', flexDirection: 'column', gap: 20 }}>
                    <div style={{ width: 50, height: 50, borderRadius: 12, background: 'var(--secondary-container)', color: 'var(--primary)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Layers size={24} />
                    </div>
                    <div>
                      <span className="caption" style={{ fontWeight: 'bold', color: 'var(--primary)', fontSize: 11, textTransform: 'uppercase' }}>Flagship Desktop Application</span>
                      <h3 style={{ fontSize: '1.4rem', marginTop: 4 }}>Service Memo &amp; Job Order Generator</h3>
                      <p style={{ marginTop: 8, fontSize: '0.95rem' }}>
                        Heavy-duty Windows client built to compile administrative job tickets, machinery labor estimates, and parts pricing logs onto precise sheets.
                      </p>
                    </div>
                    <button 
                      className="btn btn-primary" 
                      style={{ marginTop: 'auto' }}
                      onClick={() => {
                        setSelectedProduct('generator');
                        setProductView('details');
                        setShowPricingBelowApp(false);
                      }}
                    >
                      View Details &amp; Download
                    </button>
                  </div>

                  {/* Invoice & Billing Engine (Upcoming) */}
                  <div className="card glass-panel" style={{ padding: 36, display: 'flex', flexDirection: 'column', gap: 20, opacity: 0.85 }}>
                    <div style={{ width: 50, height: 50, borderRadius: 12, background: 'var(--surface-container-high)', color: 'var(--on-surface-variant)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Zap size={24} />
                    </div>
                    <div>
                      <span className="caption" style={{ fontWeight: 'bold', color: 'var(--on-surface-variant)', fontSize: 11, textTransform: 'uppercase' }}>Next-Gen Release</span>
                      <h3 style={{ fontSize: '1.4rem', marginTop: 4 }}>Invoice &amp; Billing Engine</h3>
                      <p style={{ marginTop: 8, fontSize: '0.95rem' }}>
                        Web-native billing dispatch program designed for rapid parts billing receipt generation and automated accounting client notification templates.
                      </p>
                    </div>
                    <div style={{ display: 'flex', gap: 8, marginTop: 'auto' }}>
                      <input type="text" placeholder="you@company.com" className="input-track" style={{ height: 40, fontSize: 12 }} />
                      <button className="btn btn-secondary" style={{ padding: '8px 14px', fontSize: 12 }} onClick={() => alert('Notification queued!')}>Alert Me</button>
                    </div>
                  </div>

                </div>
              </div>
            </section>
          ) : (
            // DEDICATED SERVICE MEMO & JOB ORDER GENERATOR DETAILS PAGE
            <section style={{ padding: '80px 0' }}>
              <div className="container">
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 32 }}>
                  <button className="btn btn-outline" style={{ padding: '6px 14px', fontSize: 12 }} onClick={() => setProductView('list')}>
                    ← Back to Products
                  </button>
                </div>

                <div className="hero-grid" style={{ marginBottom: 60 }}>
                  <div className="hero-content">
                    <div className="hero-badge"><Sparkles size={12} style={{ marginRight: 6 }} /> flagship {selectedProduct === 'generator' ? 'product' : 'billing suite'}</div>
                    <h2 style={{ fontSize: '2.2rem' }}>Service Memo &amp; Job Order Generator</h2>
                    <p style={{ fontSize: '1.1rem', lineHeight: 1.6 }}>
                      This dedicated Windows program empowers companies to draft clean, border-free administrative order logs, compile parts lists, and output high-contrast printed orders in half-A4 or standard layouts.
                    </p>

                    <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 12, margin: '20px 0' }}>
                      <li style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <span style={{ width: 20, height: 20, borderRadius: '50%', background: '#10b981', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: 11 }}>✓</span>
                        <span>Locked device activation seats via cryptographic licensing.</span>
                      </li>
                      <li style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <span style={{ width: 20, height: 20, borderRadius: '50%', background: '#10b981', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: 11 }}>✓</span>
                        <span>Architectural "No-Line" styling system preconfigured.</span>
                      </li>
                      <li style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <span style={{ width: 20, height: 20, borderRadius: '50%', background: '#10b981', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: 11 }}>✓</span>
                        <span>Local drafting offline with seamless optional Cloud Sync backup.</span>
                      </li>
                    </ul>

                    <div className="hero-ctas">
                      <button 
                        className="btn btn-primary" 
                        onClick={handleDownloadInstallerClick}
                        style={{ height: 48, display: 'flex', alignItems: 'center', gap: 10, padding: '0 28px' }}
                      >
                        <Download size={18} /> Download Windows Installer
                      </button>
                    </div>
                  </div>

                  {/* Mock Video Demo Display */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    <div className="video-frame-container" onClick={() => setIsVideoPlaying(!isVideoPlaying)}>
                      {isVideoPlaying ? (
                        <div style={{ position: 'absolute', width: '100%', height: '100%', background: '#090d16', zIndex: 20, display: 'flex', flexDirection: 'column', alignItems: 'center', justifySelf: 'center', justifyContent: 'center', padding: 20 }}>
                          <span style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--primary)', letterSpacing: '0.1em', marginBottom: 12 }}>WPF App Simulation Tour</span>
                          <div style={{ width: '80%', height: '55%', background: 'var(--surface-container-low)', borderRadius: 8, padding: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
                            <div style={{ height: 5, background: 'var(--outline)', width: '35%', borderRadius: 2 }}></div>
                            <div style={{ flex: 1, background: '#000', borderRadius: 4, border: '1px dashed var(--outline)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: 11 }}>
                              [ Canvas Editor Screen Share ]
                            </div>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 8 }}>
                              <span>02:10 / 03:00</span>
                              <div style={{ width: '50%', height: 3, background: 'var(--outline-variant)', borderRadius: 1 }}>
                                <div style={{ width: '70%', height: '100%', background: 'var(--primary)' }}></div>
                              </div>
                            </div>
                          </div>
                          <button className="btn btn-outline" style={{ marginTop: 12, padding: '4px 10px', fontSize: 10, borderColor: '#fff', color: '#fff' }} onClick={(e) => { e.stopPropagation(); setIsVideoPlaying(false); }}>
                            Stop Tour
                          </button>
                        </div>
                      ) : (
                        <>
                          <div className="video-play-btn"><Play size={24} style={{ marginLeft: 3 }} /></div>
                          <div className="video-overlay-text">
                            <h3 style={{ color: '#fff', fontSize: '1.1rem' }}>App walkthrough video</h3>
                            <p style={{ color: '#dbe7ff', fontSize: '0.8rem' }}>3-minute guide demonstrating licensing configuration and template sync</p>
                          </div>
                        </>
                      )}
                    </div>
                  </div>
                </div>

                {/* EXPANDABLE LICENSING TIERS (Visible upon clicking Free Download) */}
                {showPricingBelowApp && (
                  <div id="licensing-anchor" style={{ borderTop: '1px solid var(--outline-variant)', paddingTop: 60, marginTop: 40, animation: 'fadeIn 0.5s ease' }}>
                    <div className="section-header" style={{ textAlign: 'center', marginBottom: 40 }}>
                      <div className="hero-badge">Desktop Licensing Tiers</div>
                      <h2>Choose an Activation Plan</h2>
                      <p>Select your tier to secure an activation subscription key code issued by the Cloud Admin.</p>
                      <button 
                        style={{ background: 'transparent', border: 'none', color: 'var(--primary)', textDecoration: 'underline', cursor: 'pointer', marginTop: 12, fontSize: '0.95rem', fontWeight: 700 }}
                        onClick={() => {
                          const el = document.getElementById('download-section');
                          if (el) el.scrollIntoView({ behavior: 'smooth' });
                        }}
                      >
                        Already have a key? Skip to Windows Download
                      </button>
                    </div>

                    {/* TOGGLE BAR */}
                    <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 40 }}>
                      <div style={{ 
                        display: 'flex', 
                        background: 'var(--surface-container-low)', 
                        padding: '4px', 
                        borderRadius: '30px', 
                        border: '1px solid var(--outline-variant)',
                        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.05)'
                      }}>
                        <button
                          onClick={() => setPricingCategory('with_cloud')}
                          style={{
                            padding: '10px 24px',
                            borderRadius: '26px',
                            border: 'none',
                            fontWeight: 'bold',
                            fontSize: '0.95rem',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 8,
                            transition: 'all 0.3s ease',
                            background: pricingCategory === 'with_cloud' ? 'var(--primary)' : 'transparent',
                            color: pricingCategory === 'with_cloud' ? '#fff' : 'var(--on-surface-variant)',
                          }}
                        >
                          <Cloud size={16} /> With Cloud Sync
                        </button>
                        <button
                          onClick={() => setPricingCategory('without_cloud')}
                          style={{
                            padding: '10px 24px',
                            borderRadius: '26px',
                            border: 'none',
                            fontWeight: 'bold',
                            fontSize: '0.95rem',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 8,
                            transition: 'all 0.3s ease',
                            background: pricingCategory === 'without_cloud' ? 'var(--primary)' : 'transparent',
                            color: pricingCategory === 'without_cloud' ? '#fff' : 'var(--on-surface-variant)',
                          }}
                        >
                          <CloudOff size={16} /> Without Cloud (Standalone)
                        </button>
                      </div>
                    </div>

                    {pricingCategory === 'with_cloud' ? (
                      <>
                        {/* TIER 1: WITH CLOUD (BILLED MONTHLY - TOP SECTION) */}
                        <div style={{ marginBottom: 32, textAlign: 'center' }}>
                          <div style={{ display: 'inline-block', background: 'rgba(0, 79, 150, 0.1)', color: 'var(--primary)', padding: '6px 12px', borderRadius: 20, fontSize: 11, fontWeight: 'bold', textTransform: 'uppercase', marginBottom: 12 }}>
                            ☁️ HIGH-PERFORMANCE CLOUD SERVERS (BILLED MONTHLY)
                          </div>
                          <h3 style={{ fontSize: '1.75rem', fontWeight: 800, margin: '4px 0' }}>Real-Time Cloud Synchronization Plans</h3>
                          <p style={{ color: 'var(--on-surface-variant)', maxWidth: 650, margin: '8px auto 0 auto', fontSize: '0.95rem', lineHeight: 1.6 }}>
                            These plans require a recurring monthly subscription fee dedicated entirely to cloud server hosting, bandwidth, and central sync server maintenance.
                            <br/>
                            <strong style={{ color: 'var(--primary)' }}>Fixed Monthly Rates.</strong> Major version releases are optional and may induce a small upgrade charge, but only if you decide to upgrade. Otherwise, your monthly rate is fully fixed.
                          </p>
                        </div>

                        <div className="pricing-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 24, marginBottom: 40 }}>
                          {/* 50MB Cloud */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">50MB Cloud Sync</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹300</span>
                              <span className="price-period">/ month (Cloud Server Fee)</span>
                            </div>
                            
                            <div style={{ padding: '8px 12px', background: 'rgba(0, 79, 150, 0.08)', borderRadius: 12, margin: '12px 0', fontSize: '0.85rem', fontWeight: 'bold', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--primary)' }}>
                              <Cloud size={14} /> 3 Devices &amp; 50 MB Cloud Storage
                            </div>

                            <p style={{ fontSize: '0.9rem' }}>Seamless database syncing. Fits ~35,000 memos. Best for small independent shops.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Real-time active data sync</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fits ~35,000 text memos</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 3 Active Device Seats</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fixed monthly server rates</li>
                            </ul>
                            <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('cloud', '50mb_cloud')}>
                              Subscribe 50MB Cloud
                            </button>
                          </div>

                          {/* 100MB Cloud */}
                          <div className="pricing-card featured" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div style={{ display: 'inline-block', background: 'var(--primary)', color: '#fff', padding: '4px 8px', borderRadius: 12, fontSize: 10, fontWeight: 'bold', textTransform: 'uppercase', marginBottom: 12, alignSelf: 'flex-start' }}>
                              ★ Most Popular
                            </div>
                            <div className="pricing-tier">100MB Cloud Sync</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹500</span>
                              <span className="price-period">/ month (Cloud Server Fee)</span>
                            </div>

                            <div style={{ padding: '8px 12px', background: 'rgba(0, 79, 150, 0.15)', borderRadius: 12, margin: '12px 0', fontSize: '0.85rem', fontWeight: 'bold', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--primary)' }}>
                              <Cloud size={14} /> 5 Devices &amp; 100 MB Cloud Storage
                            </div>

                            <p style={{ fontSize: '0.9rem' }}>High-speed sync. Fits ~70,000 memos. Ideal for active service teams.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Real-time active data sync</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fits ~70,000 text memos</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 5 Active Device Seats</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Admin Dashboard Telemetry</li>
                            </ul>
                            <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('cloud', '100mb_cloud')}>
                              Subscribe 100MB Cloud
                            </button>
                          </div>

                          {/* 500MB Cloud */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">500MB Cloud Sync</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹1,500</span>
                              <span className="price-period">/ month (Cloud Server Fee)</span>
                            </div>

                            <div style={{ padding: '8px 12px', background: 'rgba(0, 79, 150, 0.08)', borderRadius: 12, margin: '12px 0', fontSize: '0.85rem', fontWeight: 'bold', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--primary)' }}>
                              <Cloud size={14} /> 10 Devices &amp; 500 MB Cloud Storage
                            </div>

                            <p style={{ fontSize: '0.9rem' }}>Robust storage for growing regional logistics operations.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Real-time active data sync</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fits ~350,000 text memos</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 10 Active Device Seats</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Premium DO Storage Backend</li>
                            </ul>
                            {isDigitalOceanEnabled ? (
                              <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('cloud', '500mb_cloud')}>
                                Subscribe 500MB Cloud
                              </button>
                            ) : (
                              <button className="btn btn-outline" style={{ marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }} disabled>
                                Plan Coming Soon
                              </button>
                            )}
                          </div>

                          {/* 1GB Cloud */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">1GB Cloud Sync</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹2,500</span>
                              <span className="price-period">/ month (Cloud Server Fee)</span>
                            </div>

                            <div style={{ padding: '8px 12px', background: 'rgba(0, 79, 150, 0.08)', borderRadius: 12, margin: '12px 0', fontSize: '0.85rem', fontWeight: 'bold', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--primary)' }}>
                              <Cloud size={14} /> 15 Devices &amp; 1 GB Cloud Storage
                            </div>

                            <p style={{ fontSize: '0.9rem' }}>High-priority dedicated sync speeds for larger logistics hubs.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Real-time active data sync</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fits ~700,000 text memos</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 15 Active Device Seats</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> High priority DO servers</li>
                            </ul>
                            {isDigitalOceanEnabled ? (
                              <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('cloud', '1gb_cloud')}>
                                Subscribe 1GB Cloud
                              </button>
                            ) : (
                              <button className="btn btn-outline" style={{ marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }} disabled>
                                Plan Coming Soon
                              </button>
                            )}
                          </div>

                          {/* 3GB Cloud */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">3GB Cloud Sync</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹5,000</span>
                              <span className="price-period">/ month (Cloud Server Fee)</span>
                            </div>

                            <div style={{ padding: '8px 12px', background: 'rgba(0, 79, 150, 0.08)', borderRadius: 12, margin: '12px 0', fontSize: '0.85rem', fontWeight: 'bold', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--primary)' }}>
                              <Cloud size={14} /> Unlimited Devices &amp; 3 GB Storage
                            </div>

                            <p style={{ fontSize: '0.9rem' }}>Uncapped storage and infinite device configurations for massive organizations.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Real-time active data sync</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Fits ~2.1 million memos</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Unlimited Devices Seats</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 24/7 Priority Admin Support</li>
                            </ul>
                            {isDigitalOceanEnabled ? (
                              <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('cloud', '3gb_cloud')}>
                                Subscribe 3GB Cloud
                              </button>
                            ) : (
                              <button className="btn btn-outline" style={{ marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }} disabled>
                                Plan Coming Soon
                              </button>
                            )}
                          </div>
                        </div>

                        {/* Interactive Custom Cloud Sync Plan Customizer */}
                        <div style={{ display: 'flex', justifyContent: 'center', width: '100%', marginBottom: 40 }}>
                          <div className="pricing-card featured" style={{ width: '100%', maxWidth: '800px', display: 'flex', flexDirection: 'column', gap: 16, padding: '28px' }}>
                            <div style={{ display: 'inline-block', background: 'rgba(0, 79, 150, 0.1)', color: 'var(--primary)', padding: '4px 8px', borderRadius: 12, fontSize: 10, fontWeight: 'bold', textTransform: 'uppercase', alignSelf: 'flex-start' }}>
                              ☁️ Custom cloud setup
                            </div>
                            <div className="pricing-tier">Custom Cloud Sync Plan</div>
                            <p style={{ fontSize: '0.95rem', margin: 0 }}>
                              Configure a bespoke cloud-sync license. Tailor storage space and device counts to your organization's precise scale.
                            </p>
                            
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 24, padding: '20px', background: 'var(--surface-container-low)', borderRadius: 16, border: '1px solid var(--outline-variant)' }}>
                              <div>
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', fontWeight: 'bold', marginBottom: 8 }}>
                                  <span>Device Seats Limit (max 20):</span>
                                  <span style={{ color: 'var(--primary)' }}>{customCloudDevices} Devices</span>
                                </div>
                                <input 
                                  type="range" 
                                  min="1" 
                                  max="20" 
                                  value={customCloudDevices} 
                                  onChange={(e) => setCustomCloudDevices(parseInt(e.target.value))} 
                                  style={{ width: '100%', cursor: 'pointer' }}
                                />
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--on-surface-variant)', marginTop: 4 }}>
                                  <span>1 Device</span>
                                  <span>20 Devices</span>
                                </div>
                              </div>

                              <div>
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', fontWeight: 'bold', marginBottom: 8 }}>
                                  <span>Cloud Storage Pool (max 5GB):</span>
                                  <span style={{ color: 'var(--primary)' }}>
                                    {customCloudStorage < 1.0 ? `${Math.round(customCloudStorage * 1024)} MB` : `${customCloudStorage.toFixed(2)} GB`}
                                  </span>
                                </div>
                                <input 
                                  type="range" 
                                  min="0.05" 
                                  max="5.0" 
                                  step="0.05"
                                  value={customCloudStorage} 
                                  onChange={(e) => setCustomCloudStorage(parseFloat(e.target.value))} 
                                  style={{ width: '100%', cursor: 'pointer' }}
                                />
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--on-surface-variant)', marginTop: 4 }}>
                                  <span>50 MB</span>
                                  <span>5 GB</span>
                                </div>
                              </div>
                            </div>

                            <div style={{ padding: '16px 20px', background: 'rgba(0, 79, 150, 0.05)', borderRadius: 12, borderLeft: '4px solid var(--primary)' }}>
                              <div style={{ fontSize: '1.05rem', fontWeight: 'bold', color: 'var(--primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
                                <span>Real-Time Rate:</span>
                                <span>₹{calculateCustomCloudPrice(customCloudDevices, customCloudStorage).toLocaleString('en-IN')} / month</span>
                              </div>
                              <div style={{ fontSize: '0.88rem', color: 'var(--on-surface-variant)', marginTop: 6, lineHeight: 1.5 }}>
                                💡 With a storage allocation of <strong>{customCloudStorage < 1.0 ? `${Math.round(customCloudStorage * 1024)} MB` : `${customCloudStorage.toFixed(2)} GB`}</strong>, you can store up to{' '}
                                <strong style={{ color: 'var(--primary)' }}>{Math.round(customCloudStorage * 1024 * 700).toLocaleString('en-IN')}</strong> text-only memos (without images) securely in the cloud.
                              </div>
                            </div>

                            <button 
                              className="btn btn-primary" 
                              style={{ height: 44, fontSize: '0.95rem', fontWeight: 'bold', marginTop: 8 }} 
                              onClick={() => triggerAction('cloud', { 
                                name: `Custom Cloud (${customCloudDevices} Devices, ${customCloudStorage < 1.0 ? `${Math.round(customCloudStorage * 1024)} MB` : `${customCloudStorage.toFixed(2)} GB`})`, 
                                devices: customCloudDevices, 
                                storage: customCloudStorage,
                                price: calculateCustomCloudPrice(customCloudDevices, customCloudStorage)
                              })}
                            >
                              Subscribe Custom Cloud
                            </button>
                          </div>
                        </div>
                      </>
                    ) : (
                      <>
                        {/* TIER 2: WITHOUT CLOUD (ONE-TIME PAYMENT - BOTTOM SECTION) */}
                        <div style={{ marginBottom: 32, textAlign: 'center' }}>
                          <div style={{ display: 'inline-block', background: 'rgba(100, 116, 139, 0.1)', color: 'var(--on-surface-variant)', padding: '6px 12px', borderRadius: 20, fontSize: 11, fontWeight: 'bold', textTransform: 'uppercase', marginBottom: 12 }}>
                            💻 STANDALONE LOCAL ACCESS (ONE-TIME PAYMENT)
                          </div>
                          <h3 style={{ fontSize: '1.75rem', fontWeight: 800, margin: '4px 0' }}>Standalone Local Lifetime Plans</h3>
                          <p style={{ color: 'var(--on-surface-variant)', maxWidth: 650, margin: '8px auto 0 auto', fontSize: '0.95rem', lineHeight: 1.6 }}>
                            Run MemoBud completely offline on standalone local machines. Fully offline, no internet sync or monthly costs needed!
                            <br/>
                            <strong style={{ color: '#d32f2f' }}>No cloud subscription and no cloud sync between devices is included in these plans.</strong>
                            <br/>
                            Fixed one-time local rate. Major version releases are optional—if you want to upgrade, you can pay a small amount to do so, otherwise you keep your lifetime version for free!
                          </p>
                        </div>

                        <div className="pricing-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 24, marginBottom: 40 }}>
                          {/* Free Trial Card */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div style={{ display: 'inline-block', background: 'rgba(59, 130, 246, 0.1)', color: 'var(--primary)', padding: '4px 8px', borderRadius: 12, fontSize: 10, fontWeight: 'bold', textTransform: 'uppercase', marginBottom: 12, alignSelf: 'flex-start' }}>
                              🔒 No credit card required
                            </div>
                            <div className="pricing-tier">7-Day Free Trial</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹0</span>
                              <span className="price-period">/ 7 Days</span>
                            </div>
                            <p style={{ fontSize: '0.9rem' }}>Fully functional sandbox key. Evaluate Canvas editing and local layout capabilities.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> 1 Active Standalone Seat</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> <strong>Bonus: 2 GB Cloud Sync included</strong></li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Full Canvas Designer Access</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Safe evaluation key</li>
                            </ul>
                            <button className="btn btn-outline" style={{ marginTop: 'auto' }} onClick={() => triggerAction('license', 'trial')}>
                              Get Free Trial
                            </button>
                          </div>

                          {/* Starter Local */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">Starter Local</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹15,000</span>
                              <span className="price-period">one-time</span>
                            </div>
                            <p style={{ fontSize: '0.9rem' }}>Perfect for single workstation setups and local machine operations. Paid once, yours forever.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> <strong>1 Active Standalone Seat</strong></li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No cloud sync between devices</li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No central cloud backups</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Tonal "No-Line" Layouts</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Optional major updates fee</li>
                            </ul>
                            <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('license', 'starter_local')}>
                              Purchase Starter Local
                            </button>
                          </div>

                          {/* Standard Local */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">Standard Local</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹17,000</span>
                              <span className="price-period">one-time</span>
                            </div>
                            <p style={{ fontSize: '0.9rem' }}>Deploy offline MemoBud across three concurrent computers inside your office. Paid once, yours forever.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> <strong>3 Active Standalone Seats</strong></li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No cloud sync between devices</li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No central cloud backups</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Offline administrative database</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Optional major updates fee</li>
                            </ul>
                            <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('license', 'standard_local')}>
                              Purchase Standard Local
                            </button>
                          </div>

                          {/* Professional Local */}
                          <div className="pricing-card" style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
                            <div className="pricing-tier">Professional Local</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹23,000</span>
                              <span className="price-period">one-time</span>
                            </div>
                            <p style={{ fontSize: '0.9rem' }}>High-density offline seat coverage for larger technician repair desks. Paid once, yours forever.</p>
                            <ul className="pricing-features">
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> <strong>10 Active Standalone Seats</strong></li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No cloud sync between devices</li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No central cloud backups</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Technical telemetry dashboard</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Optional major updates fee</li>
                            </ul>
                            <button className="btn btn-primary" style={{ marginTop: 'auto' }} onClick={() => triggerAction('license', 'pro_local')}>
                              Purchase Professional Local
                            </button>
                          </div>
                        </div>

                        {/* Custom Standalone Local Plan (Interactive Calculator) */}
                        <div style={{ display: 'flex', justifyContent: 'center', width: '100%', marginBottom: 40 }}>
                          <div className="pricing-card featured" style={{ width: '100%', maxWidth: '800px', display: 'flex', flexDirection: 'column', gap: 14 }}>
                            <div style={{ display: 'inline-block', background: 'rgba(100, 116, 139, 0.1)', color: 'var(--on-surface-variant)', padding: '4px 8px', borderRadius: 12, fontSize: 10, fontWeight: 'bold', textTransform: 'uppercase', alignSelf: 'flex-start' }}>
                              🛠️ Custom Terminals
                            </div>
                            <div className="pricing-tier">Custom Standalone Local Plan</div>
                            <div className="pricing-price">
                              <span className="price-amount">₹{calculateCustomLocalPrice(customLocalDevices).toLocaleString('en-IN')}</span>
                              <span className="price-period">one-time</span>
                            </div>
                            <p style={{ fontSize: '0.95rem' }}>Deploy fully offline Standalone MemoBud across customized terminal numbers with zero recurring server costs. Paid once, yours forever.</p>
                            
                            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '16px 20px', background: 'var(--surface-container-low)', borderRadius: 12, border: '1px solid var(--outline-variant)' }}>
                              <div>
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem', fontWeight: 'bold', marginBottom: 6 }}>
                                  <span>Device Seats Limit (max 20):</span>
                                  <span style={{ color: 'var(--primary)' }}>{customLocalDevices} Devices</span>
                                </div>
                                <input 
                                  type="range" 
                                  min="1" 
                                  max="20" 
                                  value={customLocalDevices} 
                                  onChange={(e) => setCustomLocalDevices(parseInt(e.target.value))} 
                                  style={{ width: '100%', cursor: 'pointer' }}
                                />
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--on-surface-variant)', marginTop: 4 }}>
                                  <span>1 Device</span>
                                  <span>20 Devices</span>
                                </div>
                              </div>
                            </div>

                            <ul className="pricing-features" style={{ margin: '8px 0' }}>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> {customLocalDevices} Standalone Active Seats</li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No cloud sync between devices</li>
                              <li className="pricing-feature" style={{ opacity: 0.65 }}><CloudOff size={13} style={{ color: '#d32f2f', marginRight: 4 }} /> No central cloud backups</li>
                              <li className="pricing-feature"><Check size={14} style={{ color: '#10b981' }} /> Optional major updates fee</li>
                            </ul>
                            <button 
                              className="btn btn-primary" 
                              style={{ height: 44, fontSize: '0.95rem', fontWeight: 'bold' }} 
                              onClick={() => triggerAction('license', { name: `Custom Local (${customLocalDevices} Devices)`, devices: customLocalDevices })}
                            >
                              Purchase Custom Standalone
                            </button>
                          </div>
                        </div>
                      </>
                    )}

                    {/* ALREADY HAVE A KEY / DOWNLOAD SECTION */}
                    <div id="download-section" style={{ marginTop: 60, padding: 40, background: 'var(--surface-container-low)', borderRadius: 16, border: '1px solid var(--outline-variant)', display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: 16 }}>
                      <h3 style={{ margin: 0, fontSize: '1.4rem' }}>Already have an activation key?</h3>
                      <p style={{ margin: 0, color: 'var(--on-surface-variant)', maxWidth: 500 }}>
                        If you've already purchased a license or have an active cloud key, you can download the latest Windows Desktop client right now.
                      </p>
                      <button 
                        className="btn btn-primary" 
                        style={{ marginTop: 10, display: 'flex', alignItems: 'center', gap: 10, padding: '0 28px', height: 48, fontSize: 15 }}
                        onClick={() => alert('Downloading desktop package: ServiceMemoGenerator_v1.4.msi')}
                      >
                        <Download size={18} /> Download Windows Installer
                      </button>
                    </div>

                  </div>
                )}

              </div>
            </section>
          )}

        </main>
      )}

      {/* 4. COMPANY ABOUT VIEW */}
      {activeTab === 'company' && (
        <main style={{ flex: 1, padding: '100px 0' }}>
          <div className="container" style={{ display: 'flex', flexDirection: 'column', gap: 60 }}>
            <div className="section-header" style={{ textAlign: 'center' }}>
              <div className="hero-badge">Our Company</div>
              <h2>MemoBud Technologies</h2>
              <p>We build low-latency high-density desktop and web interfaces to automate business administration.</p>
            </div>

            <div className="hero-grid">
              <div className="card">
                <h3>Our Product Focus</h3>
                <p>
                  MemoBud builds specialized solutions for heavy-duty business applications. Our architectural paradigms replace fragmented Excel grids with clean, beautiful layout sheets that load instantaneously.
                </p>
              </div>
              <div className="card">
                <h3>Cloud Synchronous Infrastructure</h3>
                <p>
                  By utilizing secure cryptographic locks, direct physical machine activation validation, and structured cloud backups, we ensure your data is accessible, safe, and highly synchronized.
                </p>
              </div>
            </div>
          </div>
        </main>
      )}

      {/* 5. USER CLIENT DASHBOARD VIEW */}
      {activeTab === 'dashboard' && currentUser && (
        <main style={{ flex: 1 }} className="dashboard-grid">
          
          {/* Dashboard Sidebar - Product Selector */}
          <aside className="dashboard-sidebar" style={{ background: 'var(--surface-container-low)', padding: '24px 20px', display: 'flex', flexDirection: 'column', borderRight: '1px solid var(--outline-variant)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, paddingBottom: 20, borderBottom: '1px solid var(--outline-variant)', marginBottom: 24 }}>
              <div style={{ width: 36, height: 36, borderRadius: '50%', background: 'linear-gradient(135deg, var(--primary) 0%, var(--primary-container) 100%)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: 16, fontWeight: 'bold' }}>👤</div>
              <div>
                <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--on-surface)' }}>{currentUser.name}</div>
                <div style={{ fontSize: 11, color: 'var(--on-surface-variant)' }}>{currentUser.company || 'Enterprise Partner'}</div>
              </div>
            </div>

            <button 
              className="btn btn-outline" 
              onClick={() => { setActiveTab('home'); setProductView('list'); }}
              style={{ width: '100%', marginBottom: 24, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, height: 40 }}
            >
              <span style={{ fontSize: 16 }}>←</span> Back to Website
            </button>

            <span className="caption" style={{ textTransform: 'uppercase', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', letterSpacing: '0.05em', display: 'block', marginBottom: 12 }}>
              Product Console
            </span>

            <nav className="sidebar-nav" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <button 
                className={`sidebar-btn ${selectedDashboardProduct === 'generator' ? 'active' : ''}`} 
                onClick={() => setSelectedDashboardProduct('generator')}
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderRadius: 8, border: 'none', background: selectedDashboardProduct === 'generator' ? 'var(--primary-container)' : 'transparent', color: selectedDashboardProduct === 'generator' ? 'var(--on-primary-container)' : 'var(--on-surface)', cursor: 'pointer', textAlign: 'left', fontWeight: selectedDashboardProduct === 'generator' ? 700 : 500 }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <Layers size={18} />
                  <span>Service Memo App</span>
                </div>
                <span style={{ fontSize: 9, background: 'var(--primary)', color: '#fff', padding: '2px 6px', borderRadius: 4, fontWeight: 'bold' }}>ACTIVE</span>
              </button>

               <button 
                className={`sidebar-btn ${selectedDashboardProduct === 'staff' ? 'active' : ''}`} 
                onClick={async () => { 
                  setSelectedDashboardProduct('staff'); 
                  if (userKeys.length > 0 && userKeys.some((k: any) => k.cloud_sync_enabled)) {
                    fetchStaffList(userKeys[0].id);
                    fetchStaffKey(userKeys[0].id);
                  }
                }}
                style={{ 
                  display: 'flex', 
                  alignItems: 'center', 
                  justifyContent: 'space-between', 
                  padding: '12px 16px', 
                  borderRadius: 8, 
                  border: 'none', 
                  background: selectedDashboardProduct === 'staff' ? 'var(--primary-container)' : 'transparent', 
                  color: selectedDashboardProduct === 'staff' ? 'var(--on-primary-container)' : 'var(--on-surface)', 
                  cursor: 'pointer', 
                  textAlign: 'left', 
                  fontWeight: selectedDashboardProduct === 'staff' ? 700 : 500,
                  opacity: userKeys.some((k: any) => k.cloud_sync_enabled) ? 1 : 0.7
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <User size={18} />
                  <span>Staff Management</span>
                </div>
                {userKeys.some((k: any) => k.cloud_sync_enabled) ? (
                  <span style={{ fontSize: 9, background: 'var(--primary)', color: '#fff', padding: '2px 6px', borderRadius: 4, fontWeight: 'bold' }}>LIVE</span>
                ) : (
                  <span style={{ fontSize: 9, background: '#64748b', color: '#fff', padding: '2px 6px', borderRadius: 4, fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: 2 }}>
                    🔒 CLOUD ONLY
                  </span>
                )}
              </button>

              <button 
                className={`sidebar-btn ${selectedDashboardProduct === 'billing' ? 'active' : ''}`} 
                onClick={() => setSelectedDashboardProduct('billing')}
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderRadius: 8, border: 'none', background: selectedDashboardProduct === 'billing' ? 'var(--primary-container)' : 'transparent', color: selectedDashboardProduct === 'billing' ? 'var(--on-primary-container)' : 'var(--on-surface)', cursor: 'pointer', textAlign: 'left', fontWeight: selectedDashboardProduct === 'billing' ? 700 : 500 }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <Zap size={18} />
                  <span>Invoice Engine</span>
                </div>
                <span style={{ fontSize: 9, background: 'var(--surface-container-high)', color: 'var(--on-surface-variant)', padding: '2px 6px', borderRadius: 4, fontWeight: 'bold' }}>SOON</span>
              </button>

              <button 
                className={`sidebar-btn ${selectedDashboardProduct === 'settings' ? 'active' : ''}`} 
                onClick={() => setSelectedDashboardProduct('settings')}
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderRadius: 8, border: 'none', background: selectedDashboardProduct === 'settings' ? 'var(--primary-container)' : 'transparent', color: selectedDashboardProduct === 'settings' ? 'var(--on-primary-container)' : 'var(--on-surface)', cursor: 'pointer', textAlign: 'left', fontWeight: selectedDashboardProduct === 'settings' ? 700 : 500 }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <Settings size={18} />
                  <span>Account Settings</span>
                </div>
              </button>

              {/* Admin settings button removed - moved to CloudAdmin App */}
            </nav>

            <div style={{ marginTop: 'auto', borderTop: '1px solid var(--outline-variant)', paddingTop: 24 }}>
              <button 
                className="sidebar-btn logout-accent" 
                onClick={handleLogout} 
                style={{ 
                  width: '100%', 
                  padding: '12px 16px', 
                  borderRadius: 8, 
                  border: '1px solid rgba(239, 68, 68, 0.4)', 
                  background: 'rgba(239, 68, 68, 0.08)', 
                  color: '#f87171', 
                  fontWeight: 'bold', 
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  justifyContent: 'center',
                  transition: 'all 0.2s'
                }}
              >
                <LogOut size={18} /> Exit Console Portal
              </button>
            </div>
          </aside>

          {/* Dashboard Panel Content */}
          <section className="dashboard-content" style={{ padding: '32px 40px', overflowY: 'auto' }}>
            
            {/* 1. SERVICE MEMO & JOB ORDER GENERATOR DETAILS */}
            {selectedDashboardProduct === 'generator' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 32 }}>
                
                {/* Header */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, borderBottom: '1px solid var(--outline-variant)', paddingBottom: 24 }}>
                  <div>
                    <span className="caption" style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.05em' }}>Flagship C# Desktop Client</span>
                    <h2 style={{ fontSize: '1.85rem', marginTop: 4 }}>Service Memo &amp; Job Order Generator</h2>
                    <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 2 }}>Manage active device seat activations, validity metrics, and synchronous cloud backup options.</p>
                  </div>
                  <button className="btn btn-outline" style={{ display: 'flex', alignItems: 'center', gap: 8, height: 40 }} onClick={() => alert('Downloading desktop package: ServiceMemoGenerator_v1.4.msi')}>
                    <Download size={16} /> Windows Installer
                  </button>
                </div>

                {/* Main Content Layout */}
                {userKeys.length > 0 ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
                    {userKeys.map((k) => (
                      <div key={k.id} className="card glass-panel" style={{ padding: 32, display: 'flex', flexDirection: 'column', gap: 24, border: '1px solid var(--outline-variant)' }}>
                        
                        {/* Plan Status Header */}
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
                          <div>
                            <span className="caption" style={{ textTransform: 'uppercase', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', letterSpacing: '0.05em' }}>
                              License Key Serial
                            </span>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 4 }}>
                              <span style={{ fontSize: '1.45rem', fontFamily: 'monospace', fontWeight: 800, color: 'var(--primary)', letterSpacing: '0.03em' }}>
                                {k.key_code}
                              </span>
                              <button 
                                onClick={() => copyToClipboard(k.key_code)}
                                style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--on-surface-variant)', display: 'flex', alignItems: 'center', gap: 4 }}
                                title="Copy Key"
                              >
                                <Copy size={16} />
                                {copiedKey === k.key_code && (
                                  <span style={{ fontSize: 10, color: '#10b981', fontWeight: 'bold' }}>Copied!</span>
                                )}
                              </button>
                            </div>
                          </div>

                          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                            <span style={{ background: k.is_active ? 'rgba(16, 185, 129, 0.15)' : 'rgba(239, 68, 68, 0.15)', color: k.is_active ? '#10b981' : '#ef4444', padding: '6px 12px', borderRadius: 30, fontSize: 11, fontWeight: 'bold' }}>
                              ● {k.is_active ? 'ACTIVE' : 'SUSPENDED'}
                            </span>
                            <span style={{ background: k.is_trial ? '#fbbf24' : 'var(--primary)', color: k.is_trial ? '#000' : '#fff', padding: '6px 12px', borderRadius: 30, fontSize: 11, fontWeight: 'bold' }}>
                              {k.is_trial ? '7-DAY TRIAL' : (k.subscriptions?.name || 'PREMIUM PLAN')}
                            </span>
                            <button
                              onClick={() => {
                                setSelectedPlanUpgradeKeyId(k.id);
                                setShowPlanUpgradeModal(true);
                              }}
                              style={{ 
                                padding: '12px 28px', 
                                borderRadius: '14px', 
                                fontSize: '13px', 
                                fontWeight: '800',
                                height: 'auto',
                                background: 'linear-gradient(135deg, #ff007f 0%, #7928ca 50%, #b800ff 100%)',
                                border: 'none',
                                color: '#ffffff',
                                display: 'inline-flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                gap: 8,
                                cursor: 'pointer',
                                transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
                                boxShadow: '0 4px 20px rgba(236, 72, 153, 0.4)',
                                textTransform: 'uppercase',
                                letterSpacing: '0.5px'
                              }}
                              onMouseEnter={(e) => {
                                e.currentTarget.style.transform = 'translateY(-2px) scale(1.05)';
                                e.currentTarget.style.boxShadow = '0 8px 25px rgba(236, 72, 153, 0.7)';
                              }}
                              onMouseLeave={(e) => {
                                e.currentTarget.style.transform = 'translateY(0) scale(1)';
                                e.currentTarget.style.boxShadow = '0 4px 20px rgba(236, 72, 153, 0.4)';
                              }}
                            >
                              <Sparkles size={14} style={{ filter: 'drop-shadow(0 2px 4px rgba(0,0,0,0.2))' }} /> Upgrade App Plan
                            </button>
                          </div>
                        </div>

                        {/* License Metadata Grid */}
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 20, background: 'var(--surface-container-low)', padding: 20, borderRadius: 12, border: '1px solid var(--outline-variant)' }}>
                          <div>
                            <span className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', textTransform: 'uppercase' }}>Allowed Seats</span>
                            <span style={{ fontSize: '1.2rem', fontWeight: 800, marginTop: 4, display: 'block' }}>
                              {k.devices ? k.devices.length : 0} / {k.custom_max_devices || k.subscriptions?.max_devices || 3}
                            </span>
                          </div>
                          <div>
                            <span className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', textTransform: 'uppercase' }}>Validity Limit</span>
                            <span style={{ fontSize: '1.1rem', fontWeight: 700, marginTop: 4, display: 'block', color: new Date(k.expires_at) < new Date() ? '#ef4444' : 'inherit' }}>
                              {k.expires_at ? new Date(k.expires_at).toLocaleDateString() : 'Lifetime'}
                            </span>
                          </div>
                          <div>
                            <span className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', textTransform: 'uppercase' }}>Cloud Sync Status</span>
                            <span style={{ fontSize: '1.1rem', fontWeight: 800, marginTop: 4, display: 'block', color: k.cloud_sync_enabled ? '#10b981' : '#f59e0b' }}>
                              {k.cloud_sync_enabled ? 'ENABLED' : 'DISABLED'}
                            </span>
                            {k.cloud_sync_enabled && (
                              <div>
                                <span className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', textTransform: 'uppercase' }}>Cloud Storage</span>
                                <div style={{ fontSize: '1.1rem', marginTop: 4, display: 'block' }}>
                                  {renderStorageText(k.cloud_storage_used_mb || 0, k.cloud_storage_limit_gb || 0)}
                                </div>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Cloud Sync Backup Toggle Card */}
                        <div style={{ border: '1px solid var(--outline-variant)', borderRadius: 12, padding: 24, background: k.cloud_sync_enabled ? 'rgba(59, 130, 246, 0.05)' : 'var(--surface-container-low)' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, marginBottom: 16 }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                              <div style={{ width: 40, height: 40, borderRadius: 8, background: k.cloud_sync_enabled ? 'rgba(59, 130, 246, 0.15)' : 'var(--surface-container-high)', color: k.cloud_sync_enabled ? 'var(--primary)' : 'var(--on-surface-variant)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Cloud size={20} />
                              </div>
                              <div>
                                <h4 style={{ margin: 0, fontSize: '1.05rem' }}>Cloud Sync Backup Add-on</h4>
                                <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', marginTop: 2 }}>Secure automatic estimates and template backups in the C# application.</p>
                              </div>
                            </div>
                            
                            {/* Interactive toggle switch button */}
                            <button 
                              className={`btn ${k.cloud_sync_enabled ? 'btn-outline' : 'btn-primary'}`}
                              onClick={() => {
                                if (k.cloud_sync_enabled) {
                                  setSelectedDeactivateKeyId(k.id);
                                  setDeactivateConfirmText('');
                                  setShowCloudDeactivateModal(true);
                                } else {
                                  handleUpgradeCloudSync(k.id);
                                }
                              }}
                              style={{ 
                                height: 40, 
                                padding: '0 20px', 
                                border: k.cloud_sync_enabled ? '1px solid rgba(239, 68, 68, 0.4)' : 'none', 
                                color: k.cloud_sync_enabled ? '#ef4444' : '#fff',
                                background: k.cloud_sync_enabled ? 'rgba(239, 68, 68, 0.05)' : 'var(--primary)',
                                fontWeight: 'bold' 
                              }}
                            >
                              {k.cloud_sync_enabled ? 'Deactivate Cloud Backup' : 'Upgrade to Cloud Sync'}
                            </button>
                          </div>

                          {k.cloud_sync_enabled ? (
                            <div style={{ marginTop: 20, borderTop: '1px solid var(--outline-variant)', paddingTop: 16 }}>
                              <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 8 }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem', color: 'var(--on-surface-variant)' }}>
                                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                    <Activity size={14} style={{ color: '#10b981' }} />
                                    Cloud storage: <strong>
                                      {parseFloat(k.cloud_storage_limit_gb || '0') < 1.0
                                        ? `${(k.cloud_storage_used_mb || 0).toFixed(2)} MB / ${(parseFloat(k.cloud_storage_limit_gb || '0') * 1024).toFixed(0)} MB`
                                        : `${((k.cloud_storage_used_mb || 0) / 1024).toFixed(2)} GB / ${parseFloat(k.cloud_storage_limit_gb || '0').toFixed(1)} GB`
                                      }
                                    </strong>
                                  </span>
                                </div>
                                {parseFloat(k.cloud_storage_limit_gb || '0') >= 1.0 && (
                                  <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.75rem', color: 'var(--on-surface-variant)', opacity: 0.8, marginLeft: 20 }}>
                                    <HardDrive size={12} />
                                    <span>({(k.cloud_storage_used_mb || 0).toFixed(2)} MB / ${(parseFloat(k.cloud_storage_limit_gb || '0') * 1024).toFixed(0)} MB)</span>
                                  </div>
                                )}
                              </div>
                              <div style={{ width: '100%', height: 10, background: 'var(--surface-container-high)', borderRadius: 5, overflow: 'hidden' }}>
                                <div 
                                  style={{ 
                                    width: `${Math.max(2, Math.min(100, ((k.cloud_storage_used_mb || 0) / (k.cloud_storage_limit_gb * 1024)) * 100))}%`, 
                                    height: '100%', 
                                    background: 'linear-gradient(90deg, var(--primary) 0%, var(--primary-container) 100%)',
                                    borderRadius: 5
                                  }}
                                ></div>
                              </div>
                            </div>
                          ) : (
                            <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: 0, fontStyle: 'italic' }}>
                              ⚠️ Cloud sync backups are currently disabled for this activation key. Turn on to enable real-time template coordination.
                            </p>
                          )}
                        </div>

                        {/* Active Seats Revoking Section */}
                        <div>
                          <h4 style={{ fontSize: '1.1rem', marginBottom: 12, display: 'flex', alignItems: 'center', gap: 8 }}>
                            <Laptop size={18} style={{ color: 'var(--primary)' }} /> Registered Device Seats ({k.devices ? k.devices.length : 0})
                          </h4>
                          <p style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', marginBottom: 16 }}>
                            The active seats below are authenticated to compile estimated service orders on local hardware. To revoke a computer's licensing, click Disconnect.
                          </p>

                          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                            {k.devices && k.devices.map((d: any) => (
                              <div 
                                key={d.id} 
                                style={{ 
                                  display: 'flex', 
                                  justifyContent: 'space-between', 
                                  alignItems: 'center', 
                                  padding: '16px 20px', 
                                  background: 'var(--surface-container-low)', 
                                  borderRadius: 8, 
                                  border: '1px solid var(--outline-variant)' 
                                }}
                              >
                                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                                  <div style={{ width: 8, height: 8, borderRadius: '50%', background: '#10b981' }}></div>
                                  <div style={{ display: 'flex', flexDirection: 'column' }}>
                                    <span style={{ fontSize: '0.95rem', fontWeight: 700 }}>{d.device_name || 'Active Computer'}</span>
                                    <span style={{ fontSize: '0.75rem', fontFamily: 'monospace', color: 'var(--on-surface-variant)' }}>UUID: {d.hardware_id}</span>
                                  </div>
                                </div>
                                <button 
                                  className="btn btn-outline" 
                                  onClick={() => disconnectDevice(d.id)}
                                  style={{ padding: '6px 12px', fontSize: 11, borderColor: '#ef4444', color: '#ef4444', background: 'rgba(239,68,68,0.02)' }}
                                >
                                  Disconnect
                                </button>
                              </div>
                            ))}

                            {(!k.devices || k.devices.length === 0) && (
                              <div style={{ border: '1px dashed var(--outline-variant)', borderRadius: 8, padding: '24px 0', textAlign: 'center', color: 'var(--on-surface-variant)' }}>
                                <p style={{ margin: 0, fontSize: '0.85rem' }}>No active hardware computers are currently verified under this activation key.</p>
                                <p style={{ margin: '4px 0 0 0', fontSize: '0.8rem', fontStyle: 'italic' }}>Open the MemoBud WPF client, copy your license key, and authenticate to register your local machine seat.</p>
                              </div>
                            )}
                          </div>
                        </div>

                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="card glass-panel" style={{ padding: 48, textAlign: 'center', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
                    <div style={{ width: 60, height: 60, borderRadius: '50%', background: 'var(--surface-container-high)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24 }}>🔑</div>
                    <div>
                      <h3 style={{ fontSize: '1.25rem' }}>No Flagship Licenses Issued</h3>
                      <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 6, maxWidth: 460, marginInline: 'auto' }}>
                        You must subscribe to a desktop licensing plan or initiate a 7-day sandbox evaluation trial to generate active key codes and coordinate seat limits.
                      </p>
                    </div>
                    <button className="btn btn-primary" onClick={() => { setActiveTab('products'); setProductView('list'); }}>
                      Subscribe / Get Trial License
                    </button>
                  </div>
                )}

              </div>
            )}

            {/* 3. STAFF MANAGEMENT CONSOLE */}
            {selectedDashboardProduct === 'staff' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 32 }}>
                {/* Header */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, borderBottom: '1px solid var(--outline-variant)', paddingBottom: 24 }}>
                  <div>
                    <span className="caption" style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.05em' }}>Staff Administration</span>
                    <h2 style={{ fontSize: '1.85rem', marginTop: 4 }}>Staff Management Console</h2>
                    <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 2 }}>Define and authorize field staff technicians who can modify estimates and update job orders in real-time.</p>
                  </div>
                  {userKeys.some((k: any) => k.cloud_sync_enabled) && (
                    <button className="btn btn-primary" style={{ height: 40 }} onClick={() => setShowAddStaffModal(true)}>
                      + Add Staff Member
                    </button>
                  )}
                </div>

                {!userKeys.some((k: any) => k.cloud_sync_enabled) ? (
                  /* Gated Cloud Subscription Upgrade Banner */
                  <div className="card glass-panel" style={{ padding: '40px 32px', textAlign: 'center', background: 'rgba(59, 130, 246, 0.04)', border: '1px solid rgba(59, 130, 246, 0.25)', borderRadius: 20, maxWidth: 820, margin: '10px auto 30px auto' }}>
                    <div style={{ width: 64, height: 64, borderRadius: '50%', background: 'rgba(59, 130, 246, 0.15)', color: 'var(--primary)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 20px auto' }}>
                      <Cloud size={32} />
                    </div>
                    <h3 style={{ fontSize: '1.65rem', fontWeight: 800, color: 'var(--on-surface)', marginBottom: 12 }}>
                      Staff Management via Mobile App is Available for Cloud Subscribers
                    </h3>
                    <p style={{ color: 'var(--on-surface-variant)', fontSize: '1.02rem', lineHeight: 1.6, maxWidth: 650, margin: '0 auto 28px auto' }}>
                      Empower your field technicians and shop staff to update job order statuses, record diagnostic notes, and sync repair estimates in real-time via <strong>our Mobile App</strong>. Upgrade your activation key to a Cloud Subscription to unlock Staff Management.
                    </p>
                    
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16, textAlign: 'left', marginBottom: 32 }}>
                      <div style={{ background: 'var(--surface-container-low)', padding: 18, borderRadius: 14, border: '1px solid var(--outline-variant)' }}>
                        <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--primary)', marginBottom: 4 }}>📱 Staff Mobile App</div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', lineHeight: 1.4 }}>Field staff can log in, search orders, and update repair statuses on any smartphone.</div>
                      </div>
                      <div style={{ background: 'var(--surface-container-low)', padding: 18, borderRadius: 14, border: '1px solid var(--outline-variant)' }}>
                        <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--primary)', marginBottom: 4 }}>☁️ Instant Cloud Backup</div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', lineHeight: 1.4 }}>All service memos are backed up securely to central cloud storage across devices.</div>
                      </div>
                      <div style={{ background: 'var(--surface-container-low)', padding: 18, borderRadius: 14, border: '1px solid var(--outline-variant)' }}>
                        <div style={{ fontWeight: 700, fontSize: '0.95rem', color: 'var(--primary)', marginBottom: 4 }}>🔔 Live Client Notifications</div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', lineHeight: 1.4 }}>Receive real-time unread notification cards on your desktop app when staff update jobs.</div>
                      </div>
                    </div>

                    <button 
                      className="btn btn-primary" 
                      style={{ height: 50, padding: '0 36px', fontSize: '1.05rem', fontWeight: 800, borderRadius: 12, boxShadow: '0 8px 24px rgba(59, 130, 246, 0.3)' }}
                      onClick={() => {
                        if (userKeys.length > 0) {
                          handleUpgradeCloudSync(userKeys[0].id);
                        } else {
                          window.location.hash = '#pricing';
                        }
                      }}
                    >
                      🚀 Upgrade Plan to Cloud Version
                    </button>
                  </div>
                ) : (
                  userKeys.length > 0 ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
                    {/* Activation Key Card */}
                    <div className="card glass-panel" style={{ padding: 24, background: 'rgba(0, 79, 150, 0.03)', border: '1px solid var(--outline-variant)' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16 }}>
                        <div>
                          <span className="caption" style={{ textTransform: 'uppercase', fontSize: 10, fontWeight: 700, color: 'var(--on-surface-variant)', letterSpacing: '0.05em' }}>
                            Staff Activation Key (For Mobile App Login)
                          </span>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginTop: 4 }}>
                            <span style={{ fontSize: '1.35rem', fontFamily: 'monospace', fontWeight: 800, color: 'var(--primary)', letterSpacing: '0.03em' }}>
                              {staffKeyString ? staffKeyString : 'Loading...'}
                            </span>
                            {staffKeyString && (
                            <button 
                              onClick={() => copyToClipboard(staffKeyString)}
                              style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--on-surface-variant)', display: 'flex', alignItems: 'center', gap: 4 }}
                              title="Copy Activation Key"
                            >
                              <Copy size={16} />
                              {copiedKey === staffKeyString && (
                                <span style={{ fontSize: 10, color: '#10b981', fontWeight: 'bold' }}>Copied!</span>
                              )}
                            </button>
                            )}
                          </div>
                          <span style={{ fontSize: 11, color: 'var(--on-surface-variant)', marginTop: 4, display: 'block' }}>
                            ℹ️ Your staff must enter this Activation Key on the mobile app to sync with this workstation's workspace.
                          </span>
                          <div style={{ marginTop: 12 }}>
                            <a href="/mobile.html" target="_blank" className="btn btn-outline" style={{ display: 'inline-flex', padding: '8px 16px', fontSize: 12, textDecoration: 'none', color: 'var(--primary)', borderColor: 'var(--primary)' }}>
                              📱 Open Mobile Portal
                            </a>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Staff List Table */}
                    <div className="card glass-panel" style={{ padding: 28, border: '1px solid var(--outline-variant)' }}>
                      <h3 style={{ fontSize: '1.2rem', marginBottom: 16 }}>Authorized Staff Directory ({staffList.length})</h3>
                      {staffList.length > 0 ? (
                        <div style={{ overflowX: 'auto' }}>
                          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                            <thead>
                              <tr style={{ borderBottom: '1px solid var(--outline-variant)' }}>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>NAME</th>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>EMAIL ID</th>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>PHONE NUMBER</th>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>MOBILE PASSWORD</th>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>DATE ADDED</th>
                                <th style={{ padding: '12px 16px', fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)', textAlign: 'right' }}>ACTION</th>
                              </tr>
                            </thead>
                            <tbody>
                              {staffList.map((s) => (
                                <tr key={s.id} style={{ borderBottom: '1px solid var(--outline-variant)', transition: 'background 0.2s' }}>
                                  <td style={{ padding: '16px', fontSize: 13, fontWeight: 'bold' }}>{s.name}</td>
                                  <td style={{ padding: '16px', fontSize: 13, color: 'var(--primary)', fontWeight: 600 }}>{s.email}</td>
                                  <td style={{ padding: '16px', fontSize: 13 }}>{s.phone_number}</td>
                                  <td style={{ padding: '16px', fontSize: 13, fontFamily: 'monospace', fontWeight: 600 }}>{s.password}</td>
                                  <td style={{ padding: '16px', fontSize: 12, color: 'var(--on-surface-variant)' }}>
                                    {new Date(s.created_at).toLocaleDateString()}
                                  </td>
                                  <td style={{ padding: '16px', textAlign: 'right' }}>
                                    <button 
                                      className="btn btn-outline"
                                      onClick={() => handleRemoveStaff(s.id)}
                                      style={{ padding: '6px 12px', fontSize: 11, borderColor: '#ef4444', color: '#ef4444', background: 'rgba(239, 68, 68, 0.02)' }}
                                    >
                                      Remove Staff
                                    </button>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      ) : (
                        <div style={{ border: '1px dashed var(--outline-variant)', borderRadius: 12, padding: '36px 20px', textAlign: 'center', color: 'var(--on-surface-variant)' }}>
                          <p style={{ margin: 0, fontSize: '0.9rem', fontWeight: 600 }}>No staff members registered yet.</p>
                          <p style={{ margin: '4px 0 0 0', fontSize: '0.8rem', fontStyle: 'italic' }}>Click the "+ Add Staff Member" button above to register your first technician seat.</p>
                        </div>
                      )}
                    </div>
                  </div>
                ) : (
                  <div className="card glass-panel" style={{ padding: 48, textAlign: 'center', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
                    <div style={{ width: 60, height: 60, borderRadius: '50%', background: 'var(--surface-container-high)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 24 }}>👥</div>
                    <div>
                      <h3 style={{ fontSize: '1.25rem' }}>License Key Required</h3>
                      <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 6, maxWidth: 460, marginInline: 'auto' }}>
                        You must subscribe to a plan or activate a trial license key under this account to authorize and manage staff mobile access.
                      </p>
                    </div>
                  </div>
                ))}

                {/* Add Staff Modal Overlay */}
                {showAddStaffModal && (
                  <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20 }}>
                    <div className="card glass-panel" style={{ width: '100%', maxWidth: 460, padding: 32, background: 'var(--surface-container-lowest)', border: '1px solid var(--outline-variant)', boxShadow: '0 20px 50px rgba(0,0,0,0.3)' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
                        <h3 style={{ margin: 0, fontSize: '1.3rem' }}>Add New Staff Member</h3>
                        <button onClick={() => setShowAddStaffModal(false)} style={{ background: 'transparent', border: 'none', cursor: 'pointer', fontSize: 20, color: 'var(--on-surface-variant)' }}>×</button>
                      </div>

                      <form onSubmit={handleAddStaff} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>FULL NAME</label>
                          <input 
                            type="text" 
                            required 
                            className="input-track" 
                            placeholder="e.g. John Doe"
                            value={staffName} 
                            onChange={(e) => setStaffName(e.target.value)} 
                          />
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>EMAIL ID (MOBILE LOGIN)</label>
                          <input 
                            type="email" 
                            required 
                            className="input-track" 
                            placeholder="john@company.com"
                            value={staffEmail} 
                            onChange={(e) => setStaffEmail(e.target.value)} 
                          />
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>PHONE NUMBER (ALTERNATIVE LOGIN)</label>
                          <input 
                            type="text" 
                            required 
                            className="input-track" 
                            placeholder="e.g. +123456789"
                            value={staffPhone} 
                            onChange={(e) => setStaffPhone(e.target.value)} 
                          />
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>ASSIGN MOBILE PASSWORD</label>
                          <input 
                            type="text" 
                            required 
                            className="input-track" 
                            placeholder="Assign a secure login password"
                            value={staffPassword} 
                            onChange={(e) => setStaffPassword(e.target.value)} 
                          />
                        </div>

                        <div style={{ display: 'flex', gap: 12, marginTop: 12 }}>
                          <button type="button" className="btn btn-outline" style={{ flex: 1, height: 44 }} onClick={() => setShowAddStaffModal(false)}>
                            Cancel
                          </button>
                          <button type="submit" className="btn btn-primary" style={{ flex: 1, height: 44, display: 'flex', alignItems: 'center', justifyContent: 'center' }} disabled={isSavingStaff}>
                            {isSavingStaff ? 'Registering...' : 'Register Staff'}
                          </button>
                        </div>
                      </form>
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* 2. INVOICE & BILLING ENGINE DETAILS (UPCOMING) */}
            {selectedDashboardProduct === 'billing' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 32 }}>
                
                {/* Header */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, borderBottom: '1px solid var(--outline-variant)', paddingBottom: 24 }}>
                  <div>
                    <span className="caption" style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.05em' }}>Unified Billing dispatch Suite</span>
                    <h2 style={{ fontSize: '1.85rem', marginTop: 4 }}>Invoice &amp; Billing Engine</h2>
                    <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 2 }}>Next-generation product release providing rapid invoice compiling, estimating grids, and customer templates.</p>
                  </div>
                  <span style={{ fontSize: 10, background: 'var(--surface-container-high)', color: 'var(--on-surface-variant)', padding: '6px 16px', borderRadius: 20, fontWeight: 'bold', border: '1px solid var(--outline-variant)' }}>
                    🔒 DEVELOPMENT PHASE
                  </span>
                </div>

                {/* Waitlist banner and interactive mockup */}
                <div className="hero-grid">
                  <div className="card glass-panel" style={{ padding: 32, display: 'flex', flexDirection: 'column', gap: 20 }}>
                    <div style={{ width: 44, height: 44, borderRadius: 10, background: 'rgba(245, 158, 11, 0.15)', color: '#f59e0b', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                      <Sparkles size={20} />
                    </div>
                    <div>
                      <h3 style={{ fontSize: '1.25rem' }}>Join the Priority Access Waitlist</h3>
                      <p style={{ fontSize: '0.9rem', color: 'var(--on-surface-variant)', marginTop: 8, lineHeight: 1.5 }}>
                        The Invoice Engine integrates directly into your MemoBud portal to automate client billing notifications, invoice dispatch templates, and accounting spreadsheets.
                      </p>
                    </div>

                    <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                      <input 
                        type="email" 
                        className="input-track" 
                        value={currentUser.email} 
                        readOnly 
                        style={{ height: 44, fontSize: 13, background: 'var(--surface-container-low)' }} 
                      />
                      <button className="btn btn-primary" style={{ padding: '0 20px', height: 44 }} onClick={() => alert('Outstanding! You have successfully secured slot #' + Math.floor(Math.random() * 500 + 120) + ' on the priority release queue.')}>
                        Secure Priority Slot
                      </button>
                    </div>
                    <span style={{ fontSize: 11, color: 'var(--on-surface-variant)', fontStyle: 'italic' }}>
                      ⚡ Portal partners receive automatic evaluation privileges immediately on alpha build dispatch.
                    </span>
                  </div>

                  {/* Mockup Canvas */}
                  <div className="canvas-mockup glass-panel" style={{ opacity: 0.8 }}>
                    <div className="mockup-header">
                      <div className="mockup-dots">
                        <span className="mockup-dot red"></span>
                        <span className="mockup-dot yellow"></span>
                        <span className="mockup-dot green"></span>
                      </div>
                      <span className="mockup-title">Invoice Studio Preview</span>
                    </div>
                    <div className="mockup-body" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
                      <div style={{ height: 8, background: 'var(--outline-variant)', width: '40%', borderRadius: 4 }}></div>
                      <div style={{ height: 28, background: 'var(--surface-container-low)', borderRadius: 4, border: '1px dashed var(--outline-variant)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10, fontWeight: 'bold' }}>
                        [ Unified Estimate &amp; billing Dispatcher ]
                      </div>
                      <div style={{ display: 'flex', gap: 8 }}>
                        <div style={{ flex: 1, height: 16, background: 'var(--surface-container-low)', borderRadius: 2 }}></div>
                        <div style={{ flex: 2, height: 16, background: 'var(--surface-container-low)', borderRadius: 2 }}></div>
                      </div>
                    </div>
                  </div>
                </div>

              </div>
            )}

            {selectedDashboardProduct === 'settings' && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 32, width: '100%' }}>
                
                {/* Header */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, borderBottom: '1px solid var(--outline-variant)', paddingBottom: 24 }}>
                  <div>
                    <span className="caption" style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--primary)', letterSpacing: '0.05em' }}>User Settings Console</span>
                    <h2 style={{ fontSize: '1.85rem', marginTop: 4 }}>Account Settings</h2>
                    <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 2 }}>Manage your secure credentials and cloud database operations.</p>
                  </div>
                </div>

                <div className="hero-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 24 }}>
                  
                  {/* Card 1: Change Password */}
                  <div className="card glass-panel" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20, background: 'var(--surface-container-low)', borderRadius: 12, border: '1px solid var(--outline-variant)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                      <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(59, 130, 246, 0.15)', color: '#3b82f6', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <Lock size={18} />
                      </div>
                      <h3 style={{ margin: 0, fontSize: '1.2rem' }}>Change Password</h3>
                    </div>

                    <form onSubmit={handleUpdatePasswordSettings} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>OLD PASSWORD</label>
                        <input 
                          type="password" 
                          required 
                          className="input-track" 
                          placeholder="••••••••"
                          value={oldPasswordSettings} 
                          onChange={(e) => setOldPasswordSettings(e.target.value)} 
                          style={{ height: 40, fontSize: 13 }}
                        />
                      </div>

                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>NEW PASSWORD</label>
                        <input 
                          type="password" 
                          required 
                          className="input-track" 
                          placeholder="••••••••"
                          value={newPasswordSettings} 
                          onChange={(e) => setNewPasswordSettings(e.target.value)} 
                          style={{ height: 40, fontSize: 13 }}
                        />
                      </div>

                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>CONFIRM NEW PASSWORD</label>
                        <input 
                          type="password" 
                          required 
                          className="input-track" 
                          placeholder="••••••••"
                          value={confirmNewPasswordSettings} 
                          onChange={(e) => setConfirmNewPasswordSettings(e.target.value)} 
                          style={{ height: 40, fontSize: 13 }}
                        />
                      </div>

                      <button 
                        type="submit" 
                        className="btn btn-primary" 
                        style={{ height: 40, width: '100%', marginTop: 8 }}
                        disabled={isChangingPasswordSettings}
                      >
                        {isChangingPasswordSettings ? 'Updating...' : 'Update Password'}
                      </button>
                    </form>
                  </div>

                  {/* Card 2: Clear Data */}
                  <div className="card glass-panel" style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20, background: 'var(--surface-container-low)', borderRadius: 12, border: '1px solid var(--outline-variant)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                      <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(239, 68, 68, 0.15)', color: '#ef4444', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                        <HardDrive size={18} />
                      </div>
                      <h3 style={{ margin: 0, fontSize: '1.2rem' }}>Clear Data</h3>
                    </div>

                    <p style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', lineHeight: 1.5 }}>
                      All the data in the cloud and the local storage for this account would be cleared. (A local backup copy is automatically created in your Documents folder first).
                    </p>

                    <form onSubmit={handleClearKeyDataSettings} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                        <label style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>SELECT ACTIVATION KEY</label>
                        <select 
                          required
                          value={selectedKeyToClear}
                          onChange={(e) => setSelectedKeyToClear(e.target.value)}
                          className="input-track"
                          style={{ height: 40, fontSize: 13, background: 'var(--surface-container-lowest)', color: 'var(--on-surface)' }}
                        >
                          <option value="">-- Choose Key --</option>
                          {userKeys.map((k) => (
                            <option key={k.id} value={k.id}>
                              {k.key_code} ({k.is_trial ? 'Trial' : k.subscriptions?.name || 'Basic'})
                            </option>
                          ))}
                        </select>
                      </div>

                      <div style={{ padding: 12, borderRadius: 8, background: 'rgba(239, 68, 68, 0.08)', border: '1px solid rgba(239, 68, 68, 0.2)', fontSize: '0.8rem', color: '#f87171', display: 'flex', gap: 10, alignItems: 'flex-start' }}>
                        <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: 1 }} />
                        <span>Warning: Wiping will clear the cloud database backup and delete all matching service memos inside your local desktop app database. Ensure the desktop app is open.</span>
                      </div>

                      <button 
                        type="submit" 
                        className="btn btn-outline" 
                        style={{ height: 40, width: '100%', borderColor: '#ef4444', color: '#ef4444', background: 'rgba(239, 68, 68, 0.04)', fontWeight: 'bold', marginTop: 8 }}
                        disabled={isClearingDataSettings}
                      >
                        {isClearingDataSettings ? 'Clearing Data...' : 'Wipe Account Data'}
                      </button>
                    </form>
                  </div>
                </div>

                {/* Live Progress Overlay Modal for Wiping Data */}
                {isClearingDataSettings && clearStep > 0 && (
              <div style={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                background: 'rgba(0, 0, 0, 0.75)',
                backdropFilter: 'blur(8px)',
                zIndex: 9999,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 20
              }}>
                <div className="card glass-panel" style={{
                  width: '100%',
                  maxWidth: 480,
                  background: 'var(--surface-container-low)',
                  border: '1px solid var(--outline-variant)',
                  borderRadius: 16,
                  padding: 32,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 24,
                  boxShadow: '0 20px 40px rgba(0, 0, 0, 0.4)'
                }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <div style={{ 
                      width: 24, 
                      height: 24, 
                      border: '3px solid rgba(239, 68, 68, 0.15)', 
                      borderTopColor: '#ef4444', 
                      borderRadius: '50%',
                      animation: 'spin 1s linear infinite'
                    }} />
                    <h3 style={{ margin: 0, fontSize: '1.25rem', color: 'var(--on-surface)' }}>Wiping Workspace Account Data</h3>
                  </div>

                  <p style={{ margin: 0, fontSize: '0.875rem', color: 'var(--on-surface-variant)', lineHeight: 1.5 }}>
                    Please wait while the system takes backups and clears database records locally and in the cloud. Do not close this page or shut down your desktop app.
                  </p>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                    
                    {/* Step 1: Connecting */}
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                      <span style={{ fontSize: '0.9rem', color: clearStep >= 1 ? 'var(--on-surface)' : 'var(--on-surface-variant)', fontWeight: clearStep === 1 ? 'bold' : 'normal' }}>
                        1. Connecting to local desktop client app
                      </span>
                      {clearStep === 1 && <span style={{ fontSize: '0.8rem', color: 'var(--primary)' }}>In Progress...</span>}
                      {clearStep > 1 && <span style={{ fontSize: '0.9rem', color: '#10b981', fontWeight: 'bold' }}>✓ Connected</span>}
                    </div>

                    {/* Step 2: Backup and local clear */}
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                      <span style={{ fontSize: '0.9rem', color: clearStep >= 2 ? 'var(--on-surface)' : 'var(--on-surface-variant)', fontWeight: clearStep === 2 ? 'bold' : 'normal' }}>
                        2. Taking local database backup & clearing SQLite storage
                      </span>
                      {clearStep === 2 && <span style={{ fontSize: '0.8rem', color: 'var(--primary)' }}>Saving to Documents (50%)...</span>}
                      {clearStep > 2 && <span style={{ fontSize: '0.9rem', color: '#10b981', fontWeight: 'bold' }}>✓ Backup Taken & Cleared</span>}
                    </div>

                    {/* Step 3: Cloud delete */}
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                      <span style={{ fontSize: '0.9rem', color: clearStep >= 3 ? 'var(--on-surface)' : 'var(--on-surface-variant)', fontWeight: clearStep === 3 ? 'bold' : 'normal' }}>
                        3. Purging database records in cloud storage (Supabase)
                      </span>
                      {clearStep === 3 && <span style={{ fontSize: '0.8rem', color: 'var(--primary)' }}>Deleting...</span>}
                      {clearStep > 3 && <span style={{ fontSize: '0.9rem', color: '#10b981', fontWeight: 'bold' }}>✓ Cloud Wiped</span>}
                    </div>

                    {/* Step 4: Finalizing */}
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                      <span style={{ fontSize: '0.9rem', color: clearStep >= 4 ? 'var(--on-surface)' : 'var(--on-surface-variant)', fontWeight: clearStep === 4 ? 'bold' : 'normal' }}>
                        4. Finalizing and reloading workspace UI
                      </span>
                      {clearStep === 4 && <span style={{ fontSize: '0.9rem', color: '#10b981', fontWeight: 'bold' }}>✓ Success</span>}
                    </div>

                  </div>

                  {/* Progress Bar */}
                  <div style={{ width: '100%', height: 6, background: 'var(--surface-container-highest)', borderRadius: 3, overflow: 'hidden' }}>
                    <div style={{
                      width: `${(clearStep / 4) * 100}%`,
                      height: '100%',
                      background: clearStep === 4 ? '#10b981' : '#ef4444',
                      borderRadius: 3,
                      transition: 'width 0.4s ease'
                    }} />
                  </div>
                </div>
              </div>
            )}

              </div>
            )}

            {/* Admin settings console removed - moved to CloudAdmin App */}

          </section>
        </main>
      )}

      {/* FOOTER AREA */}
      <footer style={{ borderTop: '1px solid var(--outline-variant)', background: 'var(--surface-container-low)', padding: '40px 0', marginTop: 'auto' }}>
        <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 20 }}>
          <div>
            <div style={{ fontSize: '1.1rem', fontWeight: 800, color: 'var(--primary)', marginBottom: 6 }}>MemoBud Portal</div>
            <p style={{ fontSize: '0.85rem' }}>&copy; 2026 MemoBud Technologies. All rights reserved.</p>
          </div>
          <div style={{ display: 'flex', gap: 24, fontSize: '0.9rem', color: 'var(--on-surface-variant)' }}>
            <a href="#privacy" onClick={(e) => e.preventDefault()}>Privacy Policy</a>
            <a href="#terms" onClick={(e) => e.preventDefault()}>Terms of Service</a>
            <a href="#docs" onClick={(e) => e.preventDefault()}>Developers API</a>
          </div>
        </div>
      </footer>

      {/* 1. AUTH MODAL WINDOW (GLASSMORPHIC) */}
      {showAuthModal && (
        <div className="modal-overlay" onClick={() => setShowAuthModal(false)}>
          <div className="modal-content-wrapper glass-panel" onClick={(e) => e.stopPropagation()}>
            
            <div style={{ background: 'linear-gradient(135deg, var(--primary) 0%, var(--primary-container) 100%)', padding: '32px', color: '#fff', position: 'relative' }}>
              <button 
                onClick={() => setShowAuthModal(false)}
                style={{ position: 'absolute', top: 20, right: 20, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 18, fontWeight: 'bold' }}
              >
                ✕
              </button>
              <h2 style={{ color: '#fff', fontSize: '1.5rem', fontWeight: 'bold', marginBottom: 8 }}>
                {authMode === 'login' ? 'Log in to Console' : 
                 authMode === 'signup' ? 'Create your Account' : 
                 authMode === 'forgot' ? 'Reset Password' : 
                 authMode === 'otp_verify' ? 'Verify Code (OTP)' : 
                 authMode === 'unconfirmed_email' ? 'Confirm Your Email' :
                 'Set New Password'}
              </h2>
              <p style={{ color: 'var(--on-primary-container)', fontSize: '0.85rem' }}>
                {authMode === 'login' ? 'Sign in to manage your MemoBud account' : 
                 authMode === 'signup' ? 'Sign up to access products and activate plans' : 
                 authMode === 'forgot' ? 'Enter your email and we\'ll send a 6-digit OTP code' : 
                 authMode === 'otp_verify' ? 'We have sent a verification code to your email' : 
                 authMode === 'unconfirmed_email' ? 'Click the link in your email to activate your account' :
                 'Enter your new secure password below'}
              </p>
            </div>

            <form onSubmit={handleAuth} style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 16 }}>
              
              {authMode === 'signup' && (
                <>
                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Full Name
                    </label>
                    <div style={{ position: 'relative' }}>
                      <User size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="text" 
                        placeholder="John Doe" 
                        className="input-track" 
                        value={fullName}
                        onChange={(e) => setFullName(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Company Name
                    </label>
                    <div style={{ position: 'relative' }}>
                      <Building size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="text" 
                        placeholder="Acme Billing Corp" 
                        className="input-track" 
                        value={companyName}
                        onChange={(e) => setCompanyName(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Phone Number
                    </label>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <div style={{ flex: '0 0 auto' }}>
                        <select 
                          className="input-track" 
                          value={countryCode} 
                          onChange={(e) => setCountryCode(e.target.value)}
                          style={{ height: 40, padding: '0 8px', fontSize: 13, background: 'var(--surface-container-high)', border: '1px solid var(--outline-variant)', borderRadius: 6, cursor: 'pointer' }}
                        >
                          <option value="+1">United States (+1)</option>
                          <option value="+91">India (+91)</option>
                          <option value="+44">United Kingdom (+44)</option>
                          <option value="+61">Australia (+61)</option>
                          <option value="+49">Germany (+49)</option>
                          <option value="+33">France (+33)</option>
                          <option value="+81">Japan (+81)</option>
                          <option value="+86">China (+86)</option>
                          <option value="+971">United Arab Emirates (+971)</option>
                          <option value="+65">Singapore (+65)</option>
                          <option value="+55">Brazil (+55)</option>
                          <option value="+52">Mexico (+52)</option>
                        </select>
                      </div>
                      <div style={{ position: 'relative', flex: 1 }}>
                        <Phone size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                        <input 
                          type="tel" 
                          placeholder="(555) 019-2834" 
                          className="input-track" 
                          value={phoneNumber}
                          onChange={(e) => setPhoneNumber(e.target.value.replace(/\D/g, ''))}
                          required
                          style={{ paddingLeft: 42, height: 40 }}
                        />
                      </div>
                    </div>
                  </div>
                </>
              )}

              {/* Email Address (Shown in Login, Signup, Forgot, Otp Verify, and Unconfirmed Email as read-only) */}
              {(authMode === 'login' || authMode === 'signup' || authMode === 'forgot' || authMode === 'otp_verify' || authMode === 'unconfirmed_email') && (
                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                    Email Address
                  </label>
                  <div style={{ position: 'relative' }}>
                    <Mail size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                    <input 
                      type="email" 
                      placeholder="you@company.com" 
                      className="input-track" 
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      required
                      disabled={authMode === 'otp_verify' || authMode === 'unconfirmed_email'}
                      style={{ paddingLeft: 42, height: 40, opacity: (authMode === 'otp_verify' || authMode === 'unconfirmed_email') ? 0.7 : 1 }}
                    />
                  </div>
                </div>
              )}

              {/* Check-Email Verification Alert Card */}
              {authMode === 'unconfirmed_email' && (
                <div style={{ 
                  background: 'rgba(59, 130, 246, 0.08)', 
                  border: '1px solid rgba(59, 130, 246, 0.15)', 
                  padding: '16px', 
                  borderRadius: '8px', 
                  color: 'var(--on-surface)', 
                  fontSize: '0.85rem', 
                  lineHeight: '1.5',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 8 
                }}>
                  <div style={{ fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: 6, color: 'var(--primary)' }}>
                    <span>📩 Verify Account</span>
                  </div>
                  <span>We sent a verification link to <strong>{email}</strong>. Please check your inbox and click the confirmation link to activate your account.</span>
                  <span style={{ fontSize: '0.75rem', opacity: 0.8 }}>If you didn't receive the email, check your Spam folder or click the button below to resend the verification link.</span>
                </div>
              )}

              {/* Secure Password (Login/Signup Only) */}
              {(authMode === 'login' || authMode === 'signup') && (
                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                    Secure Password
                  </label>
                  <div style={{ position: 'relative' }}>
                    <Lock size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                    <input 
                      type="password" 
                      placeholder="••••••••" 
                      className="input-track" 
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      required
                      style={{ paddingLeft: 42, height: 40 }}
                    />
                  </div>
                  {authMode === 'login' && (
                    <button 
                      type="button" 
                      onClick={() => setAuthMode('forgot')}
                      style={{ background: 'transparent', border: 'none', color: 'var(--primary)', fontSize: '0.8rem', fontWeight: 600, cursor: 'pointer', padding: '6px 0 0 0', marginTop: 2 }}
                    >
                      Forgot Password?
                    </button>
                  )}
                </div>
              )}

              {/* OTP Verification Code Input (otp_verify mode only) */}
              {authMode === 'otp_verify' && (
                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                    Verification Code (OTP)
                  </label>
                  <div style={{ position: 'relative' }}>
                    <Key size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                    <input 
                      type="text" 
                      maxLength={6}
                      placeholder="123456" 
                      className="input-track" 
                      value={otpCode}
                      onChange={(e) => setOtpCode(e.target.value.trim())}
                      required
                      style={{ paddingLeft: 42, height: 40, letterSpacing: '0.2em', fontWeight: 'bold' }}
                    />
                  </div>
                </div>
              )}

              {/* New Password Inputs (reset_password mode only) */}
              {authMode === 'reset_password' && (
                <>
                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      New Password
                    </label>
                    <div style={{ position: 'relative' }}>
                      <Lock size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="password" 
                        placeholder="••••••••" 
                        className="input-track" 
                        value={newPassword}
                        onChange={(e) => setNewPassword(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Confirm New Password
                    </label>
                    <div style={{ position: 'relative' }}>
                      <Lock size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="password" 
                        placeholder="••••••••" 
                        className="input-track" 
                        value={confirmNewPassword}
                        onChange={(e) => setConfirmNewPassword(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>
                </>
              )}

              {/* Button section & disclaimers */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 10 }}>
                {authMode === 'login' && loginLockoutTime && Date.now() < loginLockoutTime ? (
                  <button type="button" className="btn btn-primary" disabled style={{ height: 44, opacity: 0.5, cursor: 'not-allowed' }}>
                    Login Locked
                  </button>
                ) : authMode === 'forgot' && forgotLockoutTime && Date.now() < forgotLockoutTime ? (
                  <button type="button" className="btn btn-primary" disabled style={{ height: 44, opacity: 0.5, cursor: 'not-allowed' }}>
                    Reset Locked
                  </button>
                ) : (
                  <button type="submit" className="btn btn-primary" style={{ height: 44 }}>
                    {authMode === 'login' ? 'Secure Log In' : 
                     authMode === 'signup' ? 'Agree & Register' : 
                     authMode === 'forgot' ? 'Send OTP Code' : 
                     authMode === 'otp_verify' ? 'Verify Code' : 
                     authMode === 'unconfirmed_email' ? 'Resend Verification Link' :
                     'Save New Password'}
                  </button>
                )}
                
                {/* Disclaimers & rate limits display */}
                {authMode === 'login' && (
                  <div style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', textAlign: 'center', marginTop: 4 }}>
                    {loginLockoutTime && Date.now() < loginLockoutTime ? (
                      <span style={{ color: 'var(--error, #ea4335)', fontWeight: 'bold' }}>
                        Login locked. Try again in {Math.ceil((loginLockoutTime - Date.now()) / 60000)} min(s).
                      </span>
                    ) : loginAttempts > 0 ? (
                      <span style={{ color: 'var(--error, #ea4335)' }}>
                        {10 - loginAttempts} login attempts left. Lockout duration is 15 minutes.
                      </span>
                    ) : (
                      <span>Max login attempts: 10. Lockout duration: 15 minutes.</span>
                    )}
                  </div>
                )}

                {authMode === 'forgot' && (
                  <div style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', textAlign: 'center', marginTop: 4 }}>
                    {forgotLockoutTime && Date.now() < forgotLockoutTime ? (
                      <span style={{ color: 'var(--error, #ea4335)', fontWeight: 'bold' }}>
                        Forgot Password locked. Try again in {Math.ceil((forgotLockoutTime - Date.now()) / 60000)} min(s).
                      </span>
                    ) : forgotAttempts > 0 ? (
                      <span style={{ color: 'var(--error, #ea4335)' }}>
                        {10 - forgotAttempts} reset requests left. Lockout duration is 15 minutes.
                      </span>
                    ) : (
                      <span>Max forgot requests: 10. Lockout duration: 15 minutes.</span>
                    )}
                  </div>
                )}

                {(authMode === 'login' || authMode === 'signup') && (
                  <>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <div style={{ flex: 1, height: 1, background: 'var(--outline-variant)' }}></div>
                      <span style={{ fontSize: 10, color: 'var(--on-surface-variant)' }}>OR SECURE SIGN IN VIA</span>
                      <div style={{ flex: 1, height: 1, background: 'var(--outline-variant)' }}></div>
                    </div>

                    <button 
                      type="button" 
                      className="btn btn-secondary" 
                      style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10, height: 42, background: 'var(--surface-container-high)', color: 'var(--on-surface)', border: '1px solid var(--outline-variant)', fontWeight: 600 }}
                      onClick={async () => {
                        try {
                          const { error } = await supabase.auth.signInWithOAuth({
                            provider: 'google',
                            options: {
                              redirectTo: window.location.origin
                            }
                          })
                          
                          if (error) {
                            alert('Supabase Auth Error: ' + error.message)
                          }
                        } catch (err: any) {
                          alert('Google OAuth Error: ' + err.message)
                        }
                      }}
                    >
                      <svg width="18" height="18" viewBox="0 0 24 24">
                        <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" />
                        <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
                        <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z" />
                        <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z" />
                      </svg>
                      Continue with Google
                    </button>
                  </>
                )}
              </div>

              <div style={{ textAlign: 'center', marginTop: 10, fontSize: '0.85rem' }}>
                <span style={{ color: 'var(--on-surface-variant)' }}>
                  {authMode === 'forgot' || authMode === 'otp_verify' || authMode === 'reset_password' || authMode === 'unconfirmed_email' ? 'Remembered your password? ' : 
                   authMode === 'login' ? "Don't have a profile? " : 
                   'Already registered? '}
                </span>
                <button 
                  type="button" 
                  onClick={() => {
                    if (authMode === 'forgot' || authMode === 'otp_verify' || authMode === 'reset_password' || authMode === 'unconfirmed_email') {
                      setAuthMode('login');
                    } else {
                      setAuthMode(authMode === 'login' ? 'signup' : 'login');
                    }
                  }}
                  style={{ background: 'transparent', border: 'none', color: 'var(--primary)', fontWeight: 'bold', cursor: 'pointer' }}
                >
                  {authMode === 'forgot' || authMode === 'otp_verify' || authMode === 'reset_password' || authMode === 'unconfirmed_email' ? 'Back to Sign In' : 
                   authMode === 'login' ? 'Register Now' : 
                   'Sign In'}
                </button>
              </div>

            </form>

          </div>
        </div>
      )}

      {/* 1.5. FREE TRIAL REGISTRATION & ENROLLMENT MODAL */}
      {showTrialFormModal && (
        <div className="modal-overlay" onClick={() => setShowTrialFormModal(false)}>
          <div className="modal-content-wrapper glass-panel" style={{ maxWidth: 460 }} onClick={(e) => e.stopPropagation()}>
            
            <div style={{ background: 'linear-gradient(135deg, var(--primary) 0%, var(--primary-container) 100%)', padding: '24px 32px', color: '#fff', position: 'relative' }}>
              <button 
                onClick={() => setShowTrialFormModal(false)}
                style={{ position: 'absolute', top: 20, right: 20, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 16 }}
              >
                ✕
              </button>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Sparkles size={22} style={{ color: '#fbbf24' }} />
                <h3 style={{ color: '#fff', margin: 0 }}>Start your free trial today</h3>
              </div>
              <p style={{ color: 'rgba(255,255,255,0.8)', fontSize: '0.85rem', marginTop: 4 }}>7-Day Flagship App Evaluation Key</p>
            </div>

            <form onSubmit={(e) => { e.preventDefault(); handleTrialGeneration(trialName, trialEmail, trialPhone); }} style={{ padding: '28px 32px' }}>
              
              <div style={{ textAlign: 'center', marginBottom: 20 }}>
                <div style={{ display: 'inline-block', background: 'rgba(59, 130, 246, 0.1)', color: 'var(--primary)', padding: '6px 12px', borderRadius: 20, fontSize: 11, fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: '0.05em', border: '1px solid rgba(59, 130, 246, 0.2)' }}>
                  🔒 No Credit Card Required
                </div>
                <p style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', marginTop: 10 }}>
                  Enter your credentials below to generate your 7-day Service Memo App license and begin immediate download.
                </p>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6 }}>
                    Full Name
                  </label>
                  <input 
                    type="text" 
                    placeholder="Alice Smith" 
                    className="input-track"
                    value={trialName}
                    onChange={(e) => setTrialName(e.target.value)}
                    required
                  />
                </div>

                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6 }}>
                    Email ID
                  </label>
                  <input 
                    type="email" 
                    placeholder="alice@example.com" 
                    className="input-track"
                    value={trialEmail}
                    onChange={(e) => setTrialEmail(e.target.value)}
                    required
                  />
                </div>

                <div>
                  <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6 }}>
                    Phone Number
                  </label>
                  <input 
                    type="text" 
                    placeholder="+1 (555) 123-4567" 
                    className="input-track"
                    value={trialPhone}
                    onChange={(e) => setTrialPhone(e.target.value)}
                    required
                  />
                </div>

                <button 
                  type="submit" 
                  className="btn btn-primary" 
                  style={{ height: 48, marginTop: 8, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8 }}
                >
                  <Sparkles size={16} /> Generate Free Trial Key &amp; Download
                </button>

                <p style={{ fontSize: 10, color: 'var(--on-surface-variant)', textAlign: 'center', margin: '4px 0 0 0' }}>
                  By enrolling, you agree to our 7-day evaluation terms of service.
                </p>
              </div>

            </form>
          </div>
        </div>
      )}

      {/* 2. PAYMENT GATEWAY & SUCCESS RECEIPT MODAL */}
      {showPaymentModal && (
        <div className="modal-overlay" onClick={() => { if (!isProcessingPayment) setShowPaymentModal(false); }}>
          <div className="modal-content-wrapper glass-panel" style={{ maxWidth: 500 }} onClick={(e) => e.stopPropagation()}>
            
            {/* Header info */}
            <div style={{ background: 'linear-gradient(135deg, #1e293b 0%, #0f172a 100%)', padding: '24px 32px', color: '#fff', position: 'relative' }}>
              {!isProcessingPayment && (
                <button 
                  onClick={() => setShowPaymentModal(false)}
                  style={{ position: 'absolute', top: 20, right: 20, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 16 }}
                >
                  ✕
                </button>
              )}
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <LockKeyhole size={20} style={{ color: '#38bdf8' }} />
                <h3 style={{ color: '#fff', margin: 0 }}>{pendingCloudUpgrade ? 'RazorPay Secure Upgrade Gate' : 'MemoBud Cloud Billing Gate'}</h3>
              </div>
              <p style={{ color: '#94a3b8', fontSize: '0.85rem', marginTop: 4 }}>{pendingCloudUpgrade ? 'Instant Cloud sync limits upgrade portal' : 'Cryptographically sealed transaction portal'}</p>
            </div>

            {/* Success screen */}
            {paymentSuccessData ? (
              <div style={{ padding: '32px', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 24, textAlign: 'center' }}>
                <div style={{ width: 64, height: 64, borderRadius: '50%', background: 'rgba(16, 185, 129, 0.15)', color: '#10b981', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <CheckCircle size={36} />
                </div>
                <div>
                  <h3 style={{ fontSize: '1.4rem' }}>{pendingCloudUpgrade ? 'Cloud Sync Activated!' : 'Key Code Issued Successfully!'}</h3>
                  <p style={{ color: 'var(--on-surface-variant)', fontSize: '0.9rem', marginTop: 6 }}>
                    {pendingCloudUpgrade 
                      ? `Transaction successful. Your cloud backup storage has been upgraded to ${pendingCloudUpgrade.gb} GB for key:` 
                      : `Transaction successful. Your subscription activation key has been fully registered on the server and emailed to:`
                    } <strong>{currentUser?.email}</strong>.
                  </p>
                </div>

                <div style={{ width: '100%', background: 'var(--surface-container-low)', padding: '20px', borderRadius: '12px', border: '1px solid var(--outline-variant)' }}>
                  <span className="caption" style={{ textTransform: 'uppercase', fontSize: 10, fontWeight: 'bold', color: 'var(--on-surface-variant)' }}>
                    {pendingCloudUpgrade ? 'Upgraded Activation Key' : 'Issued License Key'}
                  </span>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 12, marginTop: 8 }}>
                    <span style={{ fontSize: '1.35rem', fontFamily: 'monospace', fontWeight: 800, color: 'var(--primary)', letterSpacing: '0.05em' }}>
                      {paymentSuccessData.keyCode}
                    </span>
                    <button 
                      onClick={() => copyToClipboard(paymentSuccessData.keyCode)}
                      style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--on-surface-variant)', display: 'flex' }}
                      title="Copy Key Code"
                    >
                      <Copy size={18} />
                    </button>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.8rem', color: 'var(--on-surface-variant)', marginTop: 16, paddingTop: 12, borderTop: '1px dashed var(--outline-variant)' }}>
                    <span>Plan: <strong>{paymentSuccessData.tierName}</strong></span>
                    <span>Valid To: <strong>{paymentSuccessData.expiresAt}</strong></span>
                  </div>
                </div>

                {pendingCloudUpgrade ? (
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', height: 48, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
                    onClick={() => {
                      setShowPaymentModal(false);
                      setPaymentSuccessData(null);
                      setPendingCloudUpgrade(null);
                      setActiveTab('dashboard');
                    }}
                  >
                    Return to Dashboard
                  </button>
                ) : (
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', height: 48, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10 }}
                    onClick={() => {
                      // Trigger actual installer mock download!
                      const link = document.createElement('a');
                      link.href = '#download-setup';
                      link.setAttribute('download', 'MemoBud_v1.4_Setup.msi');
                      document.body.appendChild(link);
                      // Create simulated download completion experience
                      alert('Starting Windows Installer download: MemoBud_v1.4_Setup.msi');
                      setShowPaymentModal(false);
                      setPaymentSuccessData(null);
                      setPendingAction(null);
                      setActiveTab('dashboard');
                      setSelectedDashboardProduct('generator');
                      setDashboardTab('overview');
                    }}
                  >
                    <Download size={18} /> Download Installer &amp; Go to Console
                  </button>
                )}
              </div>
            ) : checkoutStep === 'contact' ? (
              // Step 1: Contact Details Form
              <form onSubmit={handleProceedToRazorpay} style={{ padding: '28px 32px' }}>
                {/* Upgrade Order Summary */}
                {pendingCloudUpgrade && (
                  <div style={{ background: 'var(--surface-container-low)', padding: '16px', borderRadius: '12px', marginBottom: '20px', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                      <div style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--on-surface-variant)', fontWeight: 'bold' }}>Upgrade Summary</div>
                      <div style={{ fontSize: '0.95rem', fontWeight: 'bold', marginTop: 4 }}>{pendingCloudUpgrade.planName}</div>
                      <div style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', marginTop: 2 }}>Key: <span style={{ fontFamily: 'monospace' }}>{userKeys.find(k => k.id === pendingCloudUpgrade.keyId)?.key_code || pendingCloudUpgrade.keyId}</span></div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <span style={{ fontSize: '1.4rem', fontWeight: 800, color: 'var(--primary)' }}>₹{pendingCloudUpgrade.price}</span>
                      <span style={{ fontSize: '0.75rem', color: 'var(--on-surface-variant)', display: 'block' }}>/ month</span>
                    </div>
                  </div>
                )}
                
                {pendingAction && (
                  <div style={{ background: 'var(--surface-container-low)', padding: '16px', borderRadius: '12px', marginBottom: '20px', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                      <div style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--on-surface-variant)', fontWeight: 'bold' }}>Order Summary</div>
                      <div style={{ fontSize: '0.95rem', fontWeight: 'bold', marginTop: 4 }}>
                        {pendingAction.type === 'license' 
                          ? (pendingAction.tier === 'starter_local' ? 'Starter Local' : pendingAction.tier === 'standard_local' ? 'Standard Local' : pendingAction.tier === 'pro_local' ? 'Professional Local' : pendingAction.tier.name || 'Custom Local') 
                          : (pendingAction.tier === '50mb_cloud' ? '50MB Cloud Sync' : pendingAction.tier === '100mb_cloud' ? '100MB Cloud Sync' : pendingAction.tier === '500mb_cloud' ? '500MB Cloud Sync' : pendingAction.tier === '1gb_cloud' ? '1GB Cloud Sync' : pendingAction.tier === '3gb_cloud' ? '3GB Cloud Sync' : 'Cloud Sync')
                        } Plan
                      </div>
                      <div style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', marginTop: 2 }}>
                        {pendingAction.type === 'license' ? 'Lifetime Standalone Local License' : 'Monthly Sync Limit Subscription'}
                      </div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <span style={{ fontSize: '1.4rem', fontWeight: 800, color: 'var(--primary)' }}>
                        {pendingAction.type === 'license' 
                          ? `₹${pendingAction.tier === 'starter_local' ? '15,000' : pendingAction.tier === 'standard_local' ? '17,000' : pendingAction.tier === 'pro_local' ? '23,000' : calculateCustomLocalPrice(pendingAction.tier.devices).toLocaleString('en-IN')}` 
                          : `₹${pendingAction.tier === '50mb_cloud' ? '300' : pendingAction.tier === '100mb_cloud' ? '500' : pendingAction.tier === '500mb_cloud' ? '1500' : pendingAction.tier === '1gb_cloud' ? '2500' : pendingAction.tier === '3gb_cloud' ? '5000' : typeof pendingAction.tier === 'object' && pendingAction.tier.price ? pendingAction.tier.price.toLocaleString('en-IN') : '300'}`
                        }
                      </span>
                      <span style={{ fontSize: '0.75rem', color: 'var(--on-surface-variant)', display: 'block' }}>
                        {pendingAction.type === 'license' ? 'one-time' : '/ month'}
                      </span>
                    </div>
                  </div>
                )}

                <div style={{ textAlign: 'center', marginBottom: 20 }}>
                  <div style={{ display: 'inline-block', background: 'rgba(59, 130, 246, 0.1)', color: 'var(--primary)', padding: '6px 12px', borderRadius: 20, fontSize: 11, fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: '0.05em', border: '1px solid rgba(59, 130, 246, 0.2)' }}>
                    Step 1 of 2: Billing Contact Info
                  </div>
                  <p style={{ fontSize: '0.85rem', color: 'var(--on-surface-variant)', marginTop: 10 }}>
                    Please confirm the contact credentials that should be attached to this activation license.
                  </p>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Full Name
                    </label>
                    <div style={{ position: 'relative' }}>
                      <User size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="text" 
                        placeholder="Your full name" 
                        className="input-track"
                        value={billingName}
                        onChange={(e) => setBillingName(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Email Address (For license key delivery)
                    </label>
                    <div style={{ position: 'relative' }}>
                      <Mail size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="email" 
                        placeholder="you@company.com" 
                        className="input-track"
                        value={billingEmail}
                        onChange={(e) => setBillingEmail(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <div>
                    <label className="caption" style={{ display: 'block', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', marginBottom: 6, color: 'var(--on-surface-variant)' }}>
                      Phone Number
                    </label>
                    <div style={{ position: 'relative' }}>
                      <Phone size={16} style={{ position: 'absolute', left: 14, top: 12, color: 'var(--outline)' }} />
                      <input 
                        type="text" 
                        placeholder="+1 (555) 123-4567" 
                        className="input-track"
                        value={billingPhone}
                        onChange={(e) => setBillingPhone(e.target.value)}
                        required
                        style={{ paddingLeft: 42, height: 40 }}
                      />
                    </div>
                  </div>

                  <button 
                    type="submit" 
                    className="btn btn-primary" 
                    style={{ height: 48, marginTop: 12, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8 }}
                  >
                    Proceed to Secure RazorPay Checkout <ChevronRight size={18} />
                  </button>

                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6, color: 'var(--on-surface-variant)', fontSize: 11, marginTop: 8 }}>
                    <Shield size={14} style={{ color: '#10b981' }} />
                    <span>Your session is protected using absolute end-to-end SSH/HTTPS locks.</span>
                  </div>
                </div>
              </form>
            ) : (
              // Step 2: High-Fidelity RazorPay Secure Gateway simulation interface
              <div style={{ padding: '24px 32px' }}>
                
                {/* RazorPay Mockup Header */}
                <div style={{ 
                  background: '#131e3a', 
                  borderRadius: '12px', 
                  padding: '16px 20px', 
                  color: '#fff', 
                  marginBottom: '20px', 
                  border: '1px solid rgba(255,255,255,0.1)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center'
                }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ background: 'rgba(56, 189, 248, 0.2)', color: '#38bdf8', padding: '2px 8px', borderRadius: 4, fontWeight: 'bold', fontSize: 10 }}>RAZORPAY SECURE</span>
                    </div>
                    <div style={{ fontSize: '1.05rem', fontWeight: 800, marginTop: 4 }}>MemoBud Technologies</div>
                    <div style={{ fontSize: '0.8rem', color: '#94a3b8', marginTop: 2 }}>{billingEmail}</div>
                  </div>
                  
                  <div style={{ textAlign: 'right' }}>
                    <span style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'block', textTransform: 'uppercase' }}>Amount Due</span>
                    <span style={{ fontSize: '1.4rem', fontWeight: 900, color: '#38bdf8' }}>
                      ${pendingCloudUpgrade 
                        ? pendingCloudUpgrade.price.toFixed(2) 
                        : pendingAction?.type === 'license'
                          ? (pendingAction.tier === 'basic' ? '99.00' : pendingAction.tier === 'pro' ? '120.00' : pendingAction.tier === 'enterprise' ? '79.00' : '149.00')
                          : (pendingAction?.tier === 'starter_cloud' ? '5.00' : pendingAction?.tier === 'pro_cloud' ? '12.00' : pendingAction?.tier === 'biz_cloud' ? '29.00' : pendingAction?.tier === 'unlimited_cloud' ? '99.00' : '0.00')
                      }
                    </span>
                  </div>
                </div>

                {isProcessingPayment ? (
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 20, minHeight: '260px', textAlign: 'center' }}>
                    <div style={{ 
                      width: 50, 
                      height: 50, 
                      borderRadius: '50%', 
                      border: '4px solid rgba(56, 189, 248, 0.1)', 
                      borderTop: '4px solid #38bdf8', 
                      animation: 'spin 1s linear infinite' 
                    }}></div>
                    <div>
                      <h4 style={{ margin: 0, fontSize: '1.1rem', color: 'var(--on-surface)' }}>Connecting Secure Gateway...</h4>
                      <p style={{ margin: '6px 0 0 0', fontSize: '0.85rem', color: 'var(--on-surface-variant)' }}>Simulating bank verification and cryptographic confirmation...</p>
                    </div>
                    <style>{`
                      @keyframes spin {
                        0% { transform: rotate(0deg); }
                        100% { transform: rotate(360deg); }
                      }
                    `}</style>
                  </div>
                ) : (
                  <div>
                    {/* Select Payment Method Tabs */}
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8, marginBottom: 20 }}>
                      <button 
                        type="button"
                        className={`btn ${razorpayMethod === 'upi' ? 'btn-primary' : 'btn-outline'}`}
                        onClick={() => { setRazorpayMethod('upi'); }}
                        style={{ padding: '10px 0', fontSize: '0.8rem', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, height: 'auto' }}
                      >
                        ⚡ UPI / GPay
                      </button>
                      <button 
                        type="button"
                        className={`btn ${razorpayMethod === 'card' ? 'btn-primary' : 'btn-outline'}`}
                        onClick={() => { setRazorpayMethod('card'); }}
                        style={{ padding: '10px 0', fontSize: '0.8rem', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, height: 'auto' }}
                      >
                        💳 Card Payment
                      </button>
                      <button 
                        type="button"
                        className={`btn ${razorpayMethod === 'netbanking' ? 'btn-primary' : 'btn-outline'}`}
                        onClick={() => { setRazorpayMethod('netbanking'); }}
                        style={{ padding: '10px 0', fontSize: '0.8rem', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, height: 'auto' }}
                      >
                        🏦 Netbanking
                      </button>
                    </div>

                    {/* Active payment method pane */}
                    <div style={{ background: 'var(--surface-container-low)', padding: '20px', borderRadius: '12px', border: '1px solid rgba(193, 198, 212, 0.2)', marginBottom: '20px', minHeight: '180px' }}>
                      
                      {razorpayMethod === null && (
                        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', minHeight: '140px', textAlign: 'center', color: 'var(--on-surface-variant)' }}>
                          <LockKeyhole size={36} style={{ color: 'var(--outline)', marginBottom: 10 }} />
                          <p style={{ margin: 0, fontSize: '0.9rem', fontWeight: 'bold' }}>Choose a payment method above</p>
                          <p style={{ margin: '4px 0 0 0', fontSize: '0.8rem' }}>All transactions are processed securely via RazorPay sandbox simulator.</p>
                        </div>
                      )}

                      {razorpayMethod === 'upi' && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                          <span style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--on-surface-variant)', fontWeight: 'bold' }}>Pay via UPI QR Code or VPA</span>
                          
                          <div style={{ display: 'flex', alignItems: 'center', gap: 16, background: '#fff', padding: '12px', borderRadius: '8px', border: '1px solid rgba(193,198,212,0.3)' }}>
                            {/* CSS-based mockup QR Code */}
                            <div style={{ width: 80, height: 80, background: 'var(--primary-container)', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontSize: '2rem', flexShrink: 0 }}>
                              📱
                            </div>
                            <div>
                              <div style={{ fontSize: '0.85rem', fontWeight: 'bold', color: '#131e3a' }}>Scan QR Code to Pay</div>
                              <p style={{ fontSize: '0.75rem', color: '#555', margin: '4px 0 0 0' }}>Scan this dynamically allocated UPI QR using your GPay, PhonePe, or BHIM application.</p>
                            </div>
                          </div>

                          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                            <div style={{ flex: 1, height: 1, background: 'var(--outline-variant)' }}></div>
                            <span style={{ fontSize: 9, color: 'var(--on-surface-variant)' }}>OR ENTER UPI VPA</span>
                            <div style={{ flex: 1, height: 1, background: 'var(--outline-variant)' }}></div>
                          </div>

                          <div style={{ display: 'flex', gap: 8 }}>
                            <input 
                              type="text" 
                              placeholder="username@okaxis" 
                              className="input-track"
                              style={{ flex: 1, height: 40 }}
                            />
                            <button 
                              type="button" 
                              className="btn btn-primary"
                              onClick={() => { submitMockPayment(); }}
                              style={{ height: 40, whiteSpace: 'nowrap' }}
                            >
                              Pay via UPI
                            </button>
                          </div>

                          <button 
                            type="button" 
                            className="btn btn-secondary"
                            onClick={() => { submitMockPayment(); }}
                            style={{ width: '100%', height: 42, background: 'rgba(16, 185, 129, 0.15)', color: '#10b981', border: '1px solid rgba(16, 185, 129, 0.3)', fontWeight: 'bold' }}
                          >
                            Simulate QR Scan Success
                          </button>
                        </div>
                      )}

                      {razorpayMethod === 'card' && (
                        <form onSubmit={(e) => { e.preventDefault(); submitMockPayment(); }} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                          <span style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--on-surface-variant)', fontWeight: 'bold' }}>Enter Credit or Debit Card</span>
                          
                          <div>
                            <label className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                              Cardholder Name
                            </label>
                            <input 
                              type="text" 
                              placeholder="John Doe" 
                              className="input-track"
                              value={cardName}
                              onChange={(e) => setCardName(e.target.value)}
                              required
                              style={{ height: 38, fontSize: '0.85rem' }}
                            />
                          </div>

                          <div>
                            <label className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                              Card Number
                            </label>
                            <div style={{ position: 'relative' }}>
                              <CreditCard size={14} style={{ position: 'absolute', left: 12, top: 12, color: 'var(--outline)' }} />
                              <input 
                                type="text" 
                                maxLength={16}
                                placeholder="4111 2222 3333 4444" 
                                className="input-track"
                                value={cardNumber}
                                onChange={(e) => setCardNumber(e.target.value.replace(/\D/g, ''))}
                                required
                                style={{ paddingLeft: 36, height: 38, fontSize: '0.85rem' }}
                              />
                            </div>
                          </div>

                          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                            <div>
                              <label className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                                Expiry Date
                              </label>
                              <input 
                                type="text" 
                                placeholder="MM/YY" 
                                maxLength={5}
                                className="input-track"
                                value={cardExpiry}
                                onChange={(e) => setCardExpiry(e.target.value)}
                                required
                                style={{ height: 38, fontSize: '0.85rem' }}
                              />
                            </div>
                            <div>
                              <label className="caption" style={{ display: 'block', fontSize: 10, fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                                CVV Code
                              </label>
                              <input 
                                type="password" 
                                maxLength={4}
                                placeholder="•••" 
                                className="input-track"
                                value={cardCvv}
                                onChange={(e) => setCardCvv(e.target.value.replace(/\D/g, ''))}
                                required
                                style={{ height: 38, fontSize: '0.85rem' }}
                              />
                            </div>
                          </div>

                          <button 
                            type="submit" 
                            className="btn btn-primary" 
                            style={{ height: 44, marginTop: 6 }}
                          >
                            Pay Securely via Card
                          </button>
                        </form>
                      )}

                      {razorpayMethod === 'netbanking' && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                          <span style={{ fontSize: '0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--on-surface-variant)', fontWeight: 'bold' }}>Choose your netbanking institution</span>
                          
                          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, marginBottom: 6 }}>
                            <button 
                              type="button" 
                              className="btn btn-outline" 
                              onClick={() => { submitMockPayment(); }}
                              style={{ height: 42, fontSize: '0.8rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
                            >
                              🏦 State Bank of India
                            </button>
                            <button 
                              type="button" 
                              className="btn btn-outline" 
                              onClick={() => { submitMockPayment(); }}
                              style={{ height: 42, fontSize: '0.8rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
                            >
                              🏦 HDFC Bank
                            </button>
                            <button 
                              type="button" 
                              className="btn btn-outline" 
                              onClick={() => { submitMockPayment(); }}
                              style={{ height: 42, fontSize: '0.8rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
                            >
                              🏦 ICICI Bank
                            </button>
                            <button 
                              type="button" 
                              className="btn btn-outline" 
                              onClick={() => { submitMockPayment(); }}
                              style={{ height: 42, fontSize: '0.8rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
                            >
                              🏦 Axis Bank
                            </button>
                          </div>

                          <div style={{ display: 'flex', gap: 8 }}>
                            <select 
                              className="input-track"
                              style={{ flex: 1, height: 40, fontSize: '0.85rem' }}
                              defaultValue=""
                              onChange={() => { submitMockPayment(); }}
                            >
                              <option value="" disabled>-- Or Select Other Bank --</option>
                              <option value="pnb">Punjab National Bank</option>
                              <option value="boi">Bank of India</option>
                              <option value="canara">Canara Bank</option>
                              <option value="bob">Bank of Baroda</option>
                              <option value="kotak">Kotak Mahindra Bank</option>
                            </select>
                          </div>
                        </div>
                      )}

                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <button 
                        type="button"
                        onClick={() => { setCheckoutStep('contact'); }}
                        style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--primary)', fontWeight: 'bold', fontSize: '0.85rem' }}
                      >
                        ← Back to Contact Info
                      </button>
                      
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: 'var(--on-surface-variant)', fontSize: 11 }}>
                        <Shield size={14} style={{ color: '#10b981' }} />
                        <span>Secured by RazorPay Gate</span>
                      </div>
                    </div>

                  </div>
                )}

              </div>
            )}

          </div>
        </div>
      )}

      {/* 3. CLOUD SYNC UPGRADE PLAN SELECTION MODAL */}
      {showCloudUpgradeModal && selectedUpgradeKeyId && (
        <div className="modal-overlay" onClick={() => { if (!isProcessingUpgrade) setShowCloudUpgradeModal(false); }}>
          <div className="modal-content-wrapper glass-panel" style={{ maxWidth: 800, width: '95%', maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            
            <div style={{ background: 'linear-gradient(135deg, var(--primary) 0%, var(--primary-container) 100%)', padding: '28px 32px', color: '#fff', position: 'relative' }}>
              {!isProcessingUpgrade && (
                <button 
                  onClick={() => setShowCloudUpgradeModal(false)}
                  style={{ position: 'absolute', top: 20, right: 20, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 18, fontWeight: 'bold' }}
                >
                  ✕
                </button>
              )}
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Cloud size={24} style={{ color: '#60a5fa' }} />
                <h3 style={{ color: '#fff', margin: 0, fontSize: '1.45rem' }}>Upgrade to MemoBud Cloud Sync</h3>
              </div>
              <p style={{ color: 'var(--on-primary-container)', fontSize: '0.85rem', marginTop: 4 }}>
                Choose your secure cloud backup storage limit for activation key: <strong style={{ fontFamily: 'monospace', letterSpacing: '0.05em' }}>{userKeys.find(k => k.id === selectedUpgradeKeyId)?.key_code || selectedUpgradeKeyId}</strong>
              </p>
            </div>

            <div style={{ padding: '32px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 20, marginBottom: 28 }}>
                
                {/* 50MB Sync Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column', height: '100%' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>50MB Sync</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>₹300</span>
                    <span className="price-period">/ mo</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    50 MB Storage
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: '0 0 12px 0' }}>
                    3 Devices. Fits ~35,000 memos. Perfect for single independent offices.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => {
                      const currentKeyObj = userKeys.find(k => k.id === selectedUpgradeKeyId);
                      const currentLimitMb = currentKeyObj?.cloud_sync_enabled ? parseFloat(currentKeyObj.cloud_storage_limit_gb || '0') * 1024 : 0;
                      const effectiveRemainingMb = remainingFreeSpaceMb + currentLimitMb;
                      if (!isDigitalOceanEnabled && 50 > effectiveRemainingMb) {
                        alert("Warning: Insufficient server allocation capacity on free tier. Please request support to expand database capacity.");
                        return;
                      }
                      handleSelectUpgradePlan(selectedUpgradeKeyId, 0.05, '50MB Cloud Storage (3 Devices)', 300, 3);
                    }}
                    disabled={isProcessingUpgrade}
                  >
                    Select 50MB
                  </button>
                </div>

                {/* 100MB Sync Card */}
                <div className="pricing-card featured" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', display: 'flex', flexDirection: 'column', height: '100%' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem', color: 'var(--primary)' }}>100MB Sync</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>₹500</span>
                    <span className="price-period">/ mo</span>
                  </div>
                  <div style={{ background: 'var(--primary-container)', color: '#fff', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    100 MB Storage
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: '0 0 12px 0' }}>
                    5 Devices. Fits ~70,000 memos. Highly recommended for standard setups.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => {
                      const currentKeyObj = userKeys.find(k => k.id === selectedUpgradeKeyId);
                      const currentLimitMb = currentKeyObj?.cloud_sync_enabled ? parseFloat(currentKeyObj.cloud_storage_limit_gb || '0') * 1024 : 0;
                      const effectiveRemainingMb = remainingFreeSpaceMb + currentLimitMb;
                      if (!isDigitalOceanEnabled && 100 > effectiveRemainingMb) {
                        alert("Warning: Insufficient server allocation capacity on free tier. Please request support to expand database capacity.");
                        return;
                      }
                      handleSelectUpgradePlan(selectedUpgradeKeyId, 0.1, '100MB Cloud Storage (5 Devices)', 500, 5);
                    }}
                    disabled={isProcessingUpgrade}
                  >
                    Select 100MB
                  </button>
                </div>

                {/* 500MB Sync Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column', height: '100%' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>500MB Sync</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>₹1,500</span>
                    <span className="price-period">/ mo</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    500 MB Storage
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: '0 0 12px 0' }}>
                    10 Devices. Fits ~350,000 memos. Large organizational data pools.
                  </p>
                  {isDigitalOceanEnabled ? (
                    <button 
                      className="btn btn-primary" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                      onClick={() => handleSelectUpgradePlan(selectedUpgradeKeyId, 0.5, '500MB Cloud Storage (10 Devices)', 1500, 10)}
                      disabled={isProcessingUpgrade}
                    >
                      Select 500MB
                    </button>
                  ) : (
                    <button 
                      className="btn btn-outline" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }}
                      disabled
                    >
                      Coming Soon
                    </button>
                  )}
                </div>

                {/* 1GB Sync Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column', height: '100%' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>1GB Sync</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>₹2,500</span>
                    <span className="price-period">/ mo</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    1 GB Storage
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: '0 0 12px 0' }}>
                    15 Devices. Fits ~700,000 memos. Heavy operational workloads.
                  </p>
                  {isDigitalOceanEnabled ? (
                    <button 
                      className="btn btn-primary" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                      onClick={() => handleSelectUpgradePlan(selectedUpgradeKeyId, 1.0, '1GB Cloud Storage (15 Devices)', 2500, 15)}
                      disabled={isProcessingUpgrade}
                    >
                      Select 1GB
                    </button>
                  ) : (
                    <button 
                      className="btn btn-outline" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }}
                      disabled
                    >
                      Coming Soon
                    </button>
                  )}
                </div>

                {/* 3GB Sync Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column', height: '100%' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>3GB Sync</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>₹5,000</span>
                    <span className="price-period">/ mo</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    3 GB Storage
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: '0 0 12px 0' }}>
                    Unlimited Devices. Fits ~2.1M memos. Ultimate sync infrastructure.
                  </p>
                  {isDigitalOceanEnabled ? (
                    <button 
                      className="btn btn-primary" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                      onClick={() => handleSelectUpgradePlan(selectedUpgradeKeyId, 3.0, '3GB Cloud Storage (Unlimited Devices)', 5000, 9999)}
                      disabled={isProcessingUpgrade}
                    >
                      Select 3GB
                    </button>
                  ) : (
                    <button 
                      className="btn btn-outline" 
                      style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto', background: 'rgba(100,116,139,0.05)', color: 'var(--on-surface-variant)', borderColor: 'var(--outline-variant)', cursor: 'not-allowed' }}
                      disabled
                    >
                      Coming Soon
                    </button>
                  )}
                </div>

              </div>

              {isProcessingUpgrade && (
                <div style={{ textAlign: 'center', padding: '12px 0', color: 'var(--primary)', fontWeight: 'bold', fontSize: '0.95rem', animation: 'fadeIn 0.3s ease' }}>
                  ⏳ Processing Secure Upgrade Transaction...
                </div>
              )}
            </div>

          </div>
        </div>
      )}

      {/* 4. CLOUD SYNC DEACTIVATE SAFEGUARD CONFIRMATION MODAL */}
      {showCloudDeactivateModal && selectedDeactivateKeyId && (
        <div className="modal-overlay" onClick={() => setShowCloudDeactivateModal(false)}>
          <div className="modal-content-wrapper glass-panel" style={{ maxWidth: 500, width: '90%', maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            
            <div style={{ background: 'linear-gradient(135deg, #dc2626 0%, #991b1b 100%)', padding: '24px 28px', color: '#fff', position: 'relative' }}>
              <button 
                onClick={() => setShowCloudDeactivateModal(false)}
                style={{ position: 'absolute', top: 16, right: 16, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 18, fontWeight: 'bold' }}
              >
                ✕
              </button>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <CloudOff size={24} style={{ color: '#fca5a5' }} />
                <h3 style={{ color: '#fff', margin: 0, fontSize: '1.3rem' }}>Deactivate Cloud Backup</h3>
              </div>
              <p style={{ color: '#fee2e2', fontSize: '0.85rem', marginTop: 4 }}>
                Key ID: <strong style={{ fontFamily: 'monospace', letterSpacing: '0.05em' }}>{userKeys.find(k => k.id === selectedDeactivateKeyId)?.key_code || selectedDeactivateKeyId}</strong>
              </p>
            </div>

            <div style={{ padding: '28px' }}>
              <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', background: 'rgba(239, 68, 68, 0.05)', border: '1px solid rgba(239, 68, 68, 0.2)', padding: 16, borderRadius: 12, marginBottom: 20 }}>
                <AlertTriangle size={36} style={{ color: '#dc2626', flexShrink: 0 }} />
                <div>
                  <h4 style={{ margin: '0 0 4px 0', color: '#b91c1c', fontSize: '0.95rem', fontWeight: 'bold' }}>Critical Security Warning</h4>
                  <p style={{ margin: 0, fontSize: '0.85rem', color: 'var(--on-surface)', lineHeight: 1.5 }}>
                    All data till now across all devices will be lost. Once deactivated, synchronization stops immediately and the cloud-stored backups for this activation key will be permanently removed. This action is irreversible.
                  </p>
                </div>
              </div>

              <div style={{ marginBottom: 20 }}>
                <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 'bold', color: 'var(--on-surface-variant)', marginBottom: 8 }}>
                  To confirm, type <strong style={{ color: '#dc2626' }}>cancel cloud</strong> in the field below:
                </label>
                <input 
                  type="text" 
                  value={deactivateConfirmText} 
                  onChange={(e) => setDeactivateConfirmText(e.target.value)}
                  placeholder="cancel cloud"
                  className="input-track"
                  style={{ 
                    width: '100%', 
                    padding: '12px 16px', 
                    fontSize: '0.95rem', 
                    borderRadius: 8, 
                    border: deactivateConfirmText === 'cancel cloud' ? '1.5px solid #10b981' : '1.5px solid var(--outline-variant)',
                    background: 'var(--surface-container-low)',
                    color: 'var(--on-surface)',
                    boxSizing: 'border-box',
                    transition: 'all 0.2s ease',
                    fontWeight: deactivateConfirmText === 'cancel cloud' ? 'bold' : 'normal'
                  }}
                  autoFocus
                />
              </div>

              <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                <button 
                  className="btn btn-secondary" 
                  style={{ padding: '10px 20px', border: '1px solid var(--outline)', background: 'transparent', color: 'var(--on-surface)' }}
                  onClick={() => setShowCloudDeactivateModal(false)}
                >
                  Keep Backup
                </button>
                <button 
                  className="btn" 
                  disabled={deactivateConfirmText !== 'cancel cloud'}
                  onClick={async () => {
                    if (deactivateConfirmText === 'cancel cloud') {
                      setShowCloudDeactivateModal(false);
                      // Toggle off (since currentEnabled was true, toggle off by calling with true which flips it to false)
                      await handleToggleCloudSync(selectedDeactivateKeyId, true);
                    }
                  }}
                  style={{ 
                    padding: '10px 20px', 
                    background: deactivateConfirmText === 'cancel cloud' ? '#dc2626' : 'var(--surface-container-high)', 
                    color: deactivateConfirmText === 'cancel cloud' ? '#fff' : 'var(--on-surface-variant)',
                    border: 'none',
                    fontWeight: 'bold',
                    cursor: deactivateConfirmText === 'cancel cloud' ? 'pointer' : 'not-allowed',
                    transition: 'all 0.2s ease'
                  }}
                >
                  Cancel Cloud Subscription
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 5. STANDALONE LICENSE PLAN UPGRADE SELECTION MODAL */}
      {showPlanUpgradeModal && selectedPlanUpgradeKeyId && (
        <div className="modal-overlay" onClick={() => setShowPlanUpgradeModal(false)}>
          <div className="modal-content-wrapper glass-panel" style={{ maxWidth: 900, width: '95%', maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            
            <div style={{ background: 'linear-gradient(135deg, #ec4899 0%, #8b5cf6 100%)', padding: '28px 32px', color: '#fff', position: 'relative' }}>
              <button 
                onClick={() => setShowPlanUpgradeModal(false)}
                style={{ position: 'absolute', top: 20, right: 20, background: 'transparent', border: 'none', color: '#fff', cursor: 'pointer', fontSize: 18, fontWeight: 'bold' }}
              >
                ✕
              </button>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <Sparkles size={24} style={{ color: '#fbcfe8' }} />
                <h3 style={{ color: '#fff', margin: 0, fontSize: '1.45rem' }}>Upgrade Standalone App License</h3>
              </div>
              <p style={{ color: 'rgba(255,255,255,0.85)', fontSize: '0.85rem', marginTop: 4 }}>
                Choose your local license plan for activation key: <strong style={{ fontFamily: 'monospace', letterSpacing: '0.05em' }}>{userKeys.find(k => k.id === selectedPlanUpgradeKeyId)?.key_code || selectedPlanUpgradeKeyId}</strong>
              </p>
            </div>

            <div style={{ padding: '32px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 20 }}>
                
                {/* Basic Local Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>Basic Local</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>$99</span>
                    <span className="price-period">/ year</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    3 Seats Limit
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: 0 }}>
                    Perfect for independent shops and single operators.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => handleSelectPlanUpgrade(selectedPlanUpgradeKeyId, 'basic', 'Basic Local License', 99.00)}
                  >
                    Select Basic
                  </button>
                </div>

                {/* Professional Local Card */}
                <div className="pricing-card featured" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', display: 'flex', flexDirection: 'column' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem', color: 'var(--primary)' }}>Professional Local</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>$119</span>
                    <span className="price-period">/ year</span>
                  </div>
                  <div style={{ background: 'var(--primary-container)', color: '#fff', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    10 Seats Limit
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: 0 }}>
                    Our most popular mid-tier plan for expanding technician teams.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => handleSelectPlanUpgrade(selectedPlanUpgradeKeyId, 'pro', 'Professional Local License', 119.00)}
                  >
                    Select Professional
                  </button>
                </div>

                {/* Enterprise Local Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>Enterprise Local</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>$199</span>
                    <span className="price-period">/ year</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    25 Seats Limit
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: 0 }}>
                    Designed for multi-department organizations with complex needs.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => handleSelectPlanUpgrade(selectedPlanUpgradeKeyId, 'enterprise', 'Enterprise Local License', 199.00)}
                  >
                    Select Enterprise
                  </button>
                </div>

                {/* Unlimited Local Card */}
                <div className="pricing-card" style={{ padding: 24, gap: 16, background: 'var(--surface-container-lowest)', textAlign: 'center', border: '1px solid rgba(193, 198, 212, 0.2)', display: 'flex', flexDirection: 'column' }}>
                  <div className="pricing-tier" style={{ fontSize: '0.9rem' }}>Unlimited Local</div>
                  <div className="pricing-price" style={{ justifyContent: 'center' }}>
                    <span className="price-amount" style={{ fontSize: '2rem' }}>$299</span>
                    <span className="price-period">/ year</span>
                  </div>
                  <div style={{ background: 'var(--surface-container-high)', padding: '6px 12px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 'bold' }}>
                    Unlimited Seats
                  </div>
                  <p style={{ fontSize: '0.8rem', color: 'var(--on-surface-variant)', margin: 0 }}>
                    Unrestricted deployment scope. Roll out to all enterprise machines.
                  </p>
                  <button 
                    className="btn btn-primary" 
                    style={{ width: '100%', padding: '10px 14px', fontSize: 13, marginTop: 'auto' }}
                    onClick={() => handleSelectPlanUpgrade(selectedPlanUpgradeKeyId, 'unlimited', 'Unlimited Local License', 299.00)}
                  >
                    Select Unlimited
                  </button>
                </div>

              </div>
            </div>

          </div>
        </div>
      )}

    </div>
  )
}

export default App
