const gulp = require('gulp'),
    connect = require('gulp-connect'),
    uglify = require('gulp-uglify'),
    sass = require('gulp-sass'),
    watch = require('gulp-watch'),
    pug = require('gulp-pug');;

gulp.task('webserver', function () {
    connect.server({
        root: 'static',
        livereload: true,
        port: 2333
    });
});

gulp.task('pug', function () {
    return gulp.src('./static/*.pug')
        .pipe(pug({

        }))
        .pipe(gulp.dest('./static'))
        .pipe(connect.reload());
});

gulp.task('html', function () {
    return gulp.src('./static/*.html')
        .pipe(connect.reload());
});

gulp.task('js', function () {
    return gulp.src('./static/js/*.js')
        .pipe(uglify())
        .pipe(gulp.dest('./static/js'))
        .pipe(connect.reload());
});

gulp.task('sass', function () {
    return gulp.src('./static/style/*.scss')
        .pipe(sass({ outputStyle: 'expanded' }))
        .on('error', function (err) {
            console.error('sassError!', err.message);
        })
        .pipe(gulp.dest('./static/style'))
        .pipe(connect.reload());
});

gulp.task('watch', function () {
    watch('./static/**/*.js', gulp.series('js'));
    watch('./static/**/*.scss', gulp.series('sass'));
    watch('./static/**/*.pug', gulp.series('pug'));
    watch('./static/**/*.html', gulp.series('html'));
});

gulp.task('default', gulp.parallel('webserver', 'watch'));
