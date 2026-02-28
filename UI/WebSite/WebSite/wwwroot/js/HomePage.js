
(function () {
    var loader = document.getElementById('nanny-loader');
    var shown = Date.now();

    function hide() {
        var elapsed = Date.now() - shown;
        var delay = Math.max(0, 400 - elapsed); // min 400ms display
        setTimeout(function () {
            loader.classList.add('exit-fade');
            setTimeout(function () { loader.style.display = 'none'; }, 650);
        }, delay);
    }

    if (document.readyState === 'complete') hide();
    else window.addEventListener('load', hide);
})();



/* ══════════════════════════════════════════════════
   Scroll-reveal: alternating up/down per section
   ══════════════════════════════════════════════════ */
(function () {
    /* 1. Handle .reveal elements (hero already animates via CSS,
          but other .reveal below fold need JS) */
    var revealEls = document.querySelectorAll('.reveal');
    var revealObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
                revealObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.15 });
    revealEls.forEach(function (el) { revealObserver.observe(el); });

    /* 2. Handle .scroll-section — add in-view when 20% visible */
    var sections = document.querySelectorAll('.scroll-section');
    var sectionObserver = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('in-view');
                sectionObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.18 });
    sections.forEach(function (sec) { sectionObserver.observe(sec); });
})();





tailwind.config = {
    darkMode: "class",
    theme: {
        extend: {
            colors: {
                primary: "#F97316",
                primaryDark: "#EA580C",
                "background-light": "#F8F9FC",
                "background-dark": "#0F172A",
                "card-light": "#FFFFFF",
                "card-dark": "#1E293B",
                gold: "#EAB308",
            },
            fontFamily: {
                display: ["Quicksand", "sans-serif"],
                body: ["Inter", "sans-serif"],
            },
            borderRadius: {
                DEFAULT: "0.75rem",
                'sm': '0.5rem',
                'md': '0.75rem',
                'lg': '1rem',
                'xl': '1.25rem',
                '2xl': '1.5rem',
                '3xl': '2rem',
                'full': '9999px',
            },
            boxShadow: {
                'soft': '0 2px 15px -3px rgba(0,0,0,.07), 0 10px 20px -2px rgba(0,0,0,.04)',
                'card': '0 1px 3px rgba(0,0,0,.06), 0 4px 16px rgba(0,0,0,.04)',
                'glow': '0 0 24px rgba(249,115,22,.25)',
            },
        },
    },
};



