import React from 'react';
import { motion } from 'framer-motion';
import { Download, Cloud, Smartphone, QrCode, FileText, CheckCircle2, ChevronRight } from 'lucide-react';

const App: React.FC = () => {
  return (
    <div className="min-h-screen bg-background font-sans overflow-x-hidden selection:bg-primary selection:text-white">
      {/* Navigation */}
      <nav className="fixed w-full z-50 bg-white/80 backdrop-blur-md border-b border-blue-100/50">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-20">
            <div className="flex-shrink-0 flex items-center gap-2">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary to-secondary flex items-center justify-center shadow-lg shadow-primary/20">
                <FileText className="text-white w-6 h-6" />
              </div>
              <span className="font-bold text-2xl tracking-tight text-foreground">
                Joborgan
              </span>
            </div>
            <div className="hidden md:flex space-x-8">
              <a href="#features" className="text-foreground/70 hover:text-primary transition-colors font-medium">Features</a>
              <a href="#pricing" className="text-foreground/70 hover:text-primary transition-colors font-medium">Pricing</a>
            </div>
            <div className="flex items-center gap-4">
              <a href="#" className="text-sm font-medium text-primary hover:text-secondary transition-colors">Cloud Admin</a>
              <a href="#" className="text-sm font-medium text-primary hover:text-secondary transition-colors">Staff Dashboard</a>
              <motion.button 
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="bg-primary hover:bg-secondary text-white px-5 py-2.5 rounded-full font-medium transition-colors shadow-md shadow-primary/20"
              >
                Get Started
              </motion.button>
            </div>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <div className="relative pt-32 pb-20 lg:pt-48 lg:pb-32 overflow-hidden">
        {/* Watercolor Background Elements */}
        <div className="absolute top-0 left-0 w-full h-full overflow-hidden -z-10">
          <div className="absolute -top-[30%] -right-[10%] w-[70%] h-[70%] rounded-full bg-blue-100/50 blur-3xl mix-blend-multiply opacity-70" />
          <div className="absolute top-[20%] -left-[10%] w-[50%] h-[50%] rounded-full bg-teal-100/40 blur-3xl mix-blend-multiply opacity-70" />
          
          {/* Animated Wave SVG */}
          <svg className="absolute bottom-0 w-full h-[20vh] text-blue-50/50 fill-current" viewBox="0 0 1440 320" preserveAspectRatio="none">
            <path d="M0,160L48,176C96,192,192,224,288,213.3C384,203,480,149,576,144C672,139,768,181,864,186.7C960,192,1056,160,1152,149.3C1248,139,1344,149,1392,154.7L1440,160L1440,320L1392,320C1344,320,1248,320,1152,320C1056,320,960,320,864,320C768,320,672,320,576,320C480,320,384,320,288,320C192,320,96,320,48,320L0,320Z"></path>
          </svg>
        </div>

        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative text-center">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6 }}
          >
            <h1 className="text-5xl md:text-7xl font-extrabold text-foreground tracking-tight mb-6">
              The Ultimate <br className="hidden md:block" />
              <span className="text-transparent bg-clip-text bg-gradient-to-r from-primary to-teal-400">
                Job Order Generator
              </span>
            </h1>
            <p className="mt-4 text-xl md:text-2xl text-foreground/70 max-w-3xl mx-auto font-light leading-relaxed mb-10">
              Simplify your field service management. Generate invoices, sync seamlessly across devices, and empower your staff with our cutting-edge software.
            </p>
            
            <div className="flex flex-col items-center justify-center gap-3">
              <motion.button 
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="group relative flex items-center gap-3 bg-gradient-to-r from-primary to-secondary text-white px-8 py-4 rounded-full text-lg font-semibold shadow-xl shadow-primary/30 overflow-hidden"
              >
                <div className="absolute inset-0 bg-white/20 translate-y-full group-hover:translate-y-0 transition-transform duration-300 ease-out" />
                <Download className="w-5 h-5 relative z-10" />
                <span className="relative z-10">Download for Windows</span>
              </motion.button>
              <span className="text-sm text-foreground/50 font-medium">MacOS and Linux versions coming soon</span>
            </div>
          </motion.div>
        </div>
      </div>

      {/* Features Section */}
      <div id="features" className="py-24 bg-white relative">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-foreground mb-4">Powerful Core Capabilities</h2>
            <p className="text-lg text-foreground/60 max-w-2xl mx-auto">Everything you need to automate your business workflow in one elegant ecosystem.</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            <FeatureCard 
              icon={<FileText className="w-8 h-8 text-primary" />}
              title="Automated Invoicing"
              description="Instantly generate pixel-perfect job orders and invoices. Customize them with built-in Canva template parsing."
            />
            <FeatureCard 
              icon={<Cloud className="w-8 h-8 text-secondary" />}
              title="Real-Time Cloud Sync"
              description="Keep your entire team on the same page. Data syncs instantly across your local Windows client and the cloud."
            />
            <FeatureCard 
              icon={<QrCode className="w-8 h-8 text-teal-500" />}
              title="Staff Mobile App"
              description="Empower field workers with our PWA. Scan QR codes for instant job details and real-time status updates."
            />
          </div>
        </div>
      </div>

      {/* Pricing Section */}
      <div id="pricing" className="py-24 relative overflow-hidden bg-background">
        <div className="absolute inset-0 bg-gradient-to-b from-transparent to-blue-50/50" />
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-foreground mb-4">Simple, Transparent Pricing</h2>
            <p className="text-lg text-foreground/60 max-w-2xl mx-auto">Choose the tier that best fits your business scale.</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 max-w-5xl mx-auto">
            {/* Non-Cloud Tier */}
            <div className="bg-white rounded-3xl p-8 border border-gray-100 shadow-xl shadow-gray-200/50 relative overflow-hidden">
              <div className="absolute top-0 right-0 w-32 h-32 bg-gray-50 rounded-bl-full -z-10" />
              <h3 className="text-2xl font-bold text-foreground mb-2">Non-Cloud Subscription</h3>
              <p className="text-foreground/60 mb-6">Perfect for local, single-device operations.</p>
              <div className="text-4xl font-extrabold text-foreground mb-8">$29<span className="text-lg text-foreground/50 font-normal">/mo</span></div>
              
              <ul className="space-y-4 mb-8">
                <PricingFeature text="Full Windows Client Access" />
                <PricingFeature text="Local Database Storage" />
                <PricingFeature text="Unlimited Local Invoices" />
                <PricingFeature text="Custom Template Engine" />
                <PricingFeature text="Dark/Light Themes" />
              </ul>
              
              <button className="w-full py-3 px-6 rounded-xl border-2 border-primary text-primary font-semibold hover:bg-primary hover:text-white transition-colors">
                Start Free Trial
              </button>
            </div>

            {/* Cloud Tier */}
            <div className="bg-gradient-to-b from-primary to-secondary rounded-3xl p-8 shadow-2xl shadow-primary/30 relative overflow-hidden text-white transform md:-translate-y-4">
              <div className="absolute top-0 right-0 w-48 h-48 bg-white/10 rounded-bl-full -z-10 blur-xl" />
              <div className="absolute -top-4 right-8 bg-accent text-white px-4 py-1 rounded-full text-sm font-bold shadow-lg">Most Popular</div>
              <h3 className="text-2xl font-bold mb-2">Cloud Subscription</h3>
              <p className="text-white/80 mb-6">The complete ecosystem for modern teams.</p>
              <div className="text-4xl font-extrabold mb-8">$79<span className="text-lg text-white/70 font-normal">/mo</span></div>
              
              <ul className="space-y-4 mb-8 text-white/90">
                <PricingFeature text="Everything in Non-Cloud" light />
                <PricingFeature text="Real-Time Cloud Syncing" light />
                <PricingFeature text="Staff Mobile App (PWA)" light />
                <PricingFeature text="Cloud Admin Web Dashboard" light />
                <PricingFeature text="WhatsApp Integrations" light />
              </ul>
              
              <button className="w-full py-3 px-6 rounded-xl bg-white text-primary font-bold shadow-lg hover:bg-gray-50 transition-colors">
                Start Free Trial
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="bg-white border-t border-gray-100 py-12">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col md:flex-row justify-between items-center gap-6">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary to-secondary flex items-center justify-center">
              <FileText className="text-white w-4 h-4" />
            </div>
            <span className="font-bold text-xl text-foreground">Joborgan</span>
          </div>
          <p className="text-foreground/50 text-sm">© {new Date().getFullYear()} Joborgan. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
};

const FeatureCard = ({ icon, title, description }: { icon: React.ReactNode, title: string, description: string }) => (
  <motion.div 
    whileHover={{ y: -5 }}
    className="bg-white p-8 rounded-3xl border border-gray-100 shadow-xl shadow-gray-200/40 transition-all group"
  >
    <div className="w-16 h-16 rounded-2xl bg-blue-50 flex items-center justify-center mb-6 group-hover:scale-110 transition-transform">
      {icon}
    </div>
    <h3 className="text-xl font-bold text-foreground mb-3">{title}</h3>
    <p className="text-foreground/60 leading-relaxed">{description}</p>
  </motion.div>
);

const PricingFeature = ({ text, light = false }: { text: string, light?: boolean }) => (
  <li className="flex items-center gap-3">
    <CheckCircle2 className={`w-5 h-5 ${light ? 'text-white' : 'text-primary'}`} />
    <span>{text}</span>
  </li>
);

export default App;
